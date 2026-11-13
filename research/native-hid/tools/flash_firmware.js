'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const components = {
  controller: { target: 'GS_G7_Pro_CE_DV', version: '2.29', dualBackup: 1 },
  receiver: { target: 'GS_G7_Pro_CE_DG', version: '1.35', dualBackup: 0 },
};
const dllHash = '7e2ac7c2bed6266a391c830699763706d4d5628ce1676cfc31dd763b8d3c7e78';

const checkRecovery = String.raw`
$ErrorActionPreference = 'Stop'
$devices = @(Get-PnpDevice -PresentOnly)
$boot = @($devices | Where-Object { $_.InstanceId -like 'USB\VID_4C4A&PID_3442\*' })
$disk = @($devices | Where-Object { $_.InstanceId -like 'USBSTOR\DISK&VEN_BR27&PROD_UBOOT1.00&REV_1.00\*' })
$normal = @($devices | Where-Object { $_.InstanceId -match '^USB\\VID_3537&PID_(0575|10C5|10C6|10B8)\\' })
$connect = @(Get-Process -Name 'GameSir Connect' -ErrorAction SilentlyContinue |
    Where-Object { $_.Id -ne [int]$env:PANDA_FLASH_PROCESS_ID })
if ($connect.Count) { throw 'Close GameSir Connect completely before using this tool.' }
if ($boot.Count -ne 1 -or $disk.Count -ne 1 -or $normal.Count) {
    throw 'Expected one BR27 device in recovery and no other GameSir target connected.'
}
$parent = Get-PnpDeviceProperty -InstanceId $disk[0].InstanceId -KeyName DEVPKEY_Device_Parent
$driveName = $env:PANDA_FLASH_DRIVE + ':\'
$volume = @($devices | Where-Object { $_.FriendlyName -eq $driveName })
$volumeStem = 'SWD\WPDBUSENUM\_??_' + $disk[0].InstanceId.Replace('\', '#') + '#'
if ($parent.Data -ne $boot[0].InstanceId -or $volume.Count -ne 1 -or
    -not $volume[0].InstanceId.StartsWith($volumeStem, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The selected drive does not map to the BR27 recovery device.'
}
$boot[0].InstanceId
`;

function recoveryIdentity(drive) {
  try {
    return execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', checkRecovery], {
      env: { ...process.env, PANDA_FLASH_DRIVE: drive, PANDA_FLASH_PROCESS_ID: String(process.pid) },
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
      timeout: 20000,
      windowsHide: true,
    }).trim();
  } catch (error) {
    throw new Error(`Windows recovery check failed. ${error.stderr?.trim() || error.message}`);
  }
}

function sha256(data) {
  return crypto.createHash('sha256').update(data).digest('hex');
}

function requireSuccess(result, action) {
  if (result !== 0) throw new Error(`${action} failed with GameSir code ${result}.`);
}

function cString(buffer) {
  return buffer.toString('utf8').split('\0', 1)[0].trim();
}

function main() {
  const [component, drive, patchedPath, stockPath, action, ...extra] = process.argv.slice(2);
  if (!Object.prototype.hasOwnProperty.call(components, component) || !/^[D-Z]$/.test(drive || '') ||
      !patchedPath || !stockPath || (action !== undefined && action !== '--write') || extra.length) {
    throw new Error('Usage: flash_firmware.js <controller|receiver> <drive letter> <patched.ufw> <stock.ufw> [--write]');
  }
  if (process.platform !== 'win32' || process.arch !== 'ia32' ||
      path.basename(process.execPath).toLowerCase() !== 'gamesir connect.exe') {
    throw new Error('Run this script with the 32-bit GameSir Connect executable in Node mode. See the flashing guide.');
  }

  const { target, version, dualBackup } = components[component];
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'patches', `${component}.json`), 'utf8'));
  const firmware = fs.readFileSync(patchedPath);
  if (firmware.length !== manifest.size || sha256(firmware) !== manifest.result_sha256) {
    throw new Error('The patched package does not match the tested image for this component.');
  }
  if (sha256(fs.readFileSync(stockPath)) !== manifest.source_sha256) {
    throw new Error('The saved stock package does not match the source image for this component.');
  }

  const resources = path.join(path.dirname(process.execPath), 'resources');
  const dllPath = path.join(resources, 'dfu', 'jl_firmware_upgrade_x86.dll');
  if (sha256(fs.readFileSync(dllPath)) !== dllHash) {
    throw new Error('This GameSir updater DLL differs from the one used in the hardware test.');
  }
  const modules = path.join(resources, 'app.asar.unpacked', 'node_modules');
  const ffi = require(path.join(modules, 'ffi-napi'));
  const ref = require(path.join(modules, 'ref-napi'));
  const dll = ffi.Library(dllPath, {
    JL_loadUfwData: ['int', ['pointer', 'int']],
    JL_getUfwCrc: ['int', ['pointer', 'pointer']],
    JL_getUfwPidVid: ['int', ['pointer', 'int', 'pointer', 'int']],
    JL_queryDevicePidVid: ['int', ['char', 'pointer', 'int', 'pointer', 'int']],
    JL_queryDeviceDesc: ['int', ['char', 'pointer', 'int']],
    JL_setDualBackupBurning: ['int', ['int']],
    JL_upgradeDevice: ['int', ['char', 'bool', 'bool', 'pointer']],
  });

  requireSuccess(dll.JL_loadUfwData(firmware, firmware.length), 'Loading the patched package');
  const fileTarget = Buffer.alloc(16);
  const fileVendor = Buffer.alloc(4);
  requireSuccess(dll.JL_getUfwPidVid(fileTarget, fileTarget.length, fileVendor, fileVendor.length), 'Checking the package target');
  requireSuccess(dll.JL_getUfwCrc(ref.alloc('short'), ref.alloc('int')), 'Checking the package CRC');
  if (cString(fileTarget) !== target) throw new Error(`Expected package target ${target}.`);

  const recovery = recoveryIdentity(drive);
  const driveCode = drive.charCodeAt(0);
  const deviceTarget = Buffer.alloc(16);
  const deviceVersion = Buffer.alloc(4);
  const description = Buffer.alloc(64);
  requireSuccess(dll.JL_queryDevicePidVid(driveCode, deviceTarget, deviceTarget.length, deviceVersion, deviceVersion.length), 'Reading the recovery target');
  requireSuccess(dll.JL_queryDeviceDesc(driveCode, description, description.length), 'Reading the recovery description');
  if (cString(deviceTarget) !== target || cString(deviceVersion) !== version ||
      cString(description).toLowerCase() !== 'br27uboot1.00') {
    throw new Error(`Expected ${target} version ${version} in BR27 recovery. Found ${cString(deviceTarget)}, ${cString(deviceVersion)}, ${cString(description)}.`);
  }
  console.log(`Verified ${target} version ${version} on ${drive}: and the exact patched and stock packages.`);
  if (action !== '--write') {
    console.log('Preflight complete. No firmware was written.');
    return;
  }

  if (recoveryIdentity(drive) !== recovery) throw new Error('The recovery device changed during preflight.');
  requireSuccess(dll.JL_setDualBackupBurning(dualBackup), 'Setting the GameSir update mode');
  fs.writeSync(1, 'EXPERIMENTAL FLASH STARTING. Leave the target connected until the updater returns.\n');
  const result = dll.JL_upgradeDevice(driveCode, false, true, null);
  if (result !== 0) throw new Error(`The write was attempted and GameSir returned code ${result}. Check recovery before another attempt.`);
  console.log('GameSir reported success. Verify normal operation as described in the flashing guide.');
}

try {
  main();
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
