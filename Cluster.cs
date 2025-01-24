using System.Diagnostics;
namespace Constellations
{
    /// <summary>
    /// Cluster of Processes that can be run from inside a container or on a system
    /// </summary>
   public class Cluster
   {
        public string _name { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
        public List<Process> Processes { get; set; } = new List<Process>();
        public Dictionary<string, object> _locks { get; set; } = new Dictionary<string, object>();
        public List<Process> _watchers { get; set; } = new List<Process>();
        public string _configPath { get; set; }
        public Cluster() { }
        public Cluster(List<ProcessStartInfo> processes)
        {
            foreach (ProcessStartInfo info in processes)
            {
               // Processes.Add(new Process() { StartInfo = info });
            }
        }

        public void Start()
        {
            try
            {
                foreach (var process in Processes)
                {
                    _watchers.Add(process);
                    process.Start();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        public void Stop()
        {
            foreach (var process in Processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                    Console.WriteLine($"Process {process.Id} named {process.ProcessName} has exited");

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            Processes.Clear();
            _locks.Clear();
            _watchers.Clear();
        }

        public object GetOrCreateLock(string nodeName)
        {
            lock (_locks)
            {
                if (!_locks.TryGetValue(nodeName, out object lockObj))
                {
                    lockObj = new object();
                    _locks[nodeName] = lockObj;
                }
                return lockObj;
            }
        }

        public void WatchProcess(string nodeName, Process process, string programPath)
        {
            while (true)
            {
                process.WaitForExit();

                lock (GetOrCreateLock(nodeName))
                {
                    if (Processes.Contains(process))
                    {
                        Console.WriteLine($"Process '{programPath}' on node '{_name}' exited with code {process.ExitCode}");
                        Processes.Remove(process);
                        if (process.ExitCode != 0)
                        {
                            process.Start();
                        }
                        break;
                    }
                }
            }
        }
    }
}