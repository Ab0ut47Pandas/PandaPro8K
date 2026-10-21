# Assembly and original analysis

The `.s` file is the receiver's actual replacement block plus the two rate-instruction probes. The short disassembly excerpts show the reviewed modified regions. They omit the original PC's file paths.

`original/` preserves the offline analysis scripts used to reach the candidates. These are source for review, with the old workspace-relative layout retained. They require decoded firmware, startup-region maps, vendor-objdump output and assembler probes generated locally. They don't access hardware. They are not the portable release entry point.

The toolchain used `pi32v2`, CPU `r3`, from the JL703N / BR27 vendor SDK. The assembly command was:

```text
clang -target pi32v2 -mcpu=r3 -c receiver-native-rate.s -o receiver-native-rate.o
llvm-objdump -d -mcpu=r3 -print-imm-hex receiver-native-rate.o
```

Full SDK/toolchain binaries and GameSir application images aren't included. The exact XOR patcher in `../tools/patch_firmware.py` reproduces the tested full-package hashes without needing the toolchain or research directory structure.

The receiver candidate validation in `../evidence/receiver-candidate-validation.json` records the decoded native hat targets, reachable instruction checks, preserved-register tests and 1,625 axis-vector checks. That file was created before flashing, so its original status says offline/not flashed. The subsequent hardware result is recorded in `../evidence/final-validation.json` and `../docs/results.md`.
