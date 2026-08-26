using System.Runtime.InteropServices;
using System.Security.Principal;

namespace PandaPro8K;

internal static class StartupManager
{
    private const string TaskName = "Panda Pro 8K";
    private const string IgnoreVariable = "SDL_GAMECONTROLLER_IGNORE_DEVICES";
    private static readonly string[] GameSirDevices =
    {
        "0x3537/0x10C5", // wired
        "0x3537/0x10C6", // 2.4 GHz dongle
    };
    private const int CreateOrUpdate = 6;
    private const int InteractiveToken = 3;
    private const int HighestRunLevel = 1;
    private const int LogonTrigger = 9;
    private const int ExecuteAction = 0;
    private const int IgnoreNewInstance = 2;

    public static bool IsEnabled(string executablePath)
    {
        return TaskPointsTo(executablePath) && IsGameSirIgnored();
    }

    public static bool IsGameSirIgnored() => IgnoreListContainsGameSir();

    public static void Enable(string executablePath)
    {
        CreateOrUpdateTask(executablePath);
        try
        {
            UpdateIgnoreList(add: true);
        }
        catch
        {
            TryDeleteTask();
            throw;
        }
    }

    public static void Disable()
    {
        Exception? taskError = null;
        try { DeleteTask(); }
        catch (Exception ex) { taskError = ex; }

        UpdateIgnoreList(add: false);
        if (taskError is not null) throw taskError;
    }

    private static bool TaskPointsTo(string executablePath)
    {
        dynamic? service = null;
        dynamic? root = null;
        dynamic? registeredTask = null;
        dynamic? definition = null;
        dynamic? actions = null;
        dynamic? action = null;
        try
        {
            service = CreateTaskService();
            root = service.GetFolder("\\");
            registeredTask = root.GetTask(TaskName);
            definition = registeredTask.Definition;
            actions = definition.Actions;
            action = actions.Item(1);
            string configuredPath = action.Path;
            return string.Equals(
                Path.GetFullPath(configuredPath),
                Path.GetFullPath(executablePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            Release(action);
            Release(actions);
            Release(definition);
            Release(registeredTask);
            Release(root);
            Release(service);
        }
    }

    private static void CreateOrUpdateTask(string executablePath)
    {
        dynamic? service = null;
        dynamic? root = null;
        dynamic? definition = null;
        dynamic? registration = null;
        dynamic? settings = null;
        dynamic? principal = null;
        dynamic? triggers = null;
        dynamic? trigger = null;
        dynamic? actions = null;
        dynamic? action = null;
        dynamic? registeredTask = null;
        try
        {
            service = CreateTaskService();
            root = service.GetFolder("\\");
            definition = service.NewTask(0);

            registration = definition.RegistrationInfo;
            registration.Description = "Starts the Panda Pro 8K controller bridge when this user signs in.";

            settings = definition.Settings;
            settings.Enabled = true;
            settings.AllowDemandStart = true;
            settings.StartWhenAvailable = true;
            settings.DisallowStartIfOnBatteries = false;
            settings.StopIfGoingOnBatteries = false;
            settings.ExecutionTimeLimit = "PT0S";
            settings.MultipleInstances = IgnoreNewInstance;

            string userName = WindowsIdentity.GetCurrent().Name;
            principal = definition.Principal;
            principal.UserId = userName;
            principal.LogonType = InteractiveToken;
            principal.RunLevel = HighestRunLevel;

            triggers = definition.Triggers;
            trigger = triggers.Create(LogonTrigger);
            trigger.Id = "UserLogon";
            trigger.UserId = userName;

            actions = definition.Actions;
            action = actions.Create(ExecuteAction);
            action.Id = "StartPanda";
            action.Path = executablePath;
            action.Arguments = "--startup";
            action.WorkingDirectory = Path.GetDirectoryName(executablePath);

            registeredTask = root.RegisterTaskDefinition(
                TaskName,
                definition,
                CreateOrUpdate,
                userName,
                null,
                InteractiveToken,
                null);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException("Windows could not create the logon task.", ex);
        }
        finally
        {
            Release(registeredTask);
            Release(action);
            Release(actions);
            Release(trigger);
            Release(triggers);
            Release(principal);
            Release(settings);
            Release(registration);
            Release(definition);
            Release(root);
            Release(service);
        }
    }

    private static void DeleteTask()
    {
        dynamic? service = null;
        dynamic? root = null;
        try
        {
            service = CreateTaskService();
            root = service.GetFolder("\\");
            root.DeleteTask(TaskName, 0);
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80070002)
        {
            // Already absent.
        }
        finally
        {
            Release(root);
            Release(service);
        }
    }

    private static void TryDeleteTask()
    {
        try { DeleteTask(); } catch { }
    }

    private static dynamic CreateTaskService()
    {
        Type type = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Windows Task Scheduler is unavailable.");
        dynamic service = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Windows Task Scheduler could not be started.");
        service.Connect();
        return service;
    }

    private static bool IgnoreListContainsGameSir()
    {
        string? value = Environment.GetEnvironmentVariable(IgnoreVariable, EnvironmentVariableTarget.User);
        List<string> entries = ParseIgnoreList(value);
        return GameSirDevices.All(device => entries.Contains(device, StringComparer.OrdinalIgnoreCase));
    }

    private static void UpdateIgnoreList(bool add)
    {
        string? current = Environment.GetEnvironmentVariable(IgnoreVariable, EnvironmentVariableTarget.User);
        if (add && current?.TrimStart().StartsWith('@') == true)
        {
            throw new InvalidOperationException(
                $"{IgnoreVariable} currently points to a file. Add {string.Join(" and ", GameSirDevices)} to that file manually before enabling Windows startup.");
        }
        List<string> entries = ParseIgnoreList(current);
        entries.RemoveAll(item => GameSirDevices.Contains(item, StringComparer.OrdinalIgnoreCase));
        if (add) entries.AddRange(GameSirDevices);

        string? updated = entries.Count == 0 ? null : string.Join(',', entries);
        Environment.SetEnvironmentVariable(IgnoreVariable, updated, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(IgnoreVariable, updated, EnvironmentVariableTarget.Process);
        BroadcastEnvironmentChange();
    }

    private static List<string> ParseIgnoreList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }

    private static void BroadcastEnvironmentChange()
    {
        SendMessageTimeout(
            new IntPtr(0xFFFF),
            0x001A,
            IntPtr.Zero,
            "Environment",
            0x0002,
            5000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
