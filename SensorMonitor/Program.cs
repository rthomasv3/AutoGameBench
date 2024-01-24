using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CoreHook.IPC.Messages;
using CoreHook.IPC.NamedPipes;
using LibreHardwareMonitor.Hardware;

namespace SensorMonitor;

internal class Program
{
    #region Fields

    private static INamedPipe _pipe;
    private static Computer _computer;
    private static bool _shouldSendSensors;

    #endregion

    #region Public Methods

    public static void Main(string[] args)
    {
        _pipe = new NamedPipeClient("AutoGameBenchSensorMonitor");
        _pipe.Connect();

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
        _computer.Open();

        Log("Waiting For Start Command");

        IMessage message = _pipe.MessageHandler.Read();

        if (String.Equals(message.Header, "command", StringComparison.OrdinalIgnoreCase) &&
            String.Equals(message.Body, "start", StringComparison.OrdinalIgnoreCase))
        {
            _shouldSendSensors = true;
        }

        Log("Starting Thread");

        Thread sensorThread = new Thread(CollectSensorData);
        sensorThread.Start();

        message = _pipe.MessageHandler.Read();

        if (String.Equals(message.Header, "command", StringComparison.OrdinalIgnoreCase) &&
            String.Equals(message.Body, "stop", StringComparison.OrdinalIgnoreCase))
        {
            _shouldSendSensors = false;
        }

        Log("Stopping Thread");

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (sensorThread.ThreadState == System.Threading.ThreadState.Running && 
               stopwatch.ElapsedMilliseconds < 10000)
        {
            Thread.Sleep(100);
        }
        stopwatch.Stop();

        Log("Stopped Thread");

        if (_pipe != null)
        {
            _pipe.Dispose();
            _pipe = null;
        }

        if (_computer != null)
        {
            _computer.Close();
            _computer = null;
        }

        Log("Cleanup Complete");
    }

    #endregion

    #region Private Methods

    private static void CollectSensorData()
    {
        while (_shouldSendSensors)
        {
            try
            {
                _computer.Accept(new UpdateVisitor());

                foreach (IHardware hardware in _computer.Hardware)
                {
                    foreach (IHardware subhardware in hardware.SubHardware)
                    {
                        foreach (ISensor sensor in subhardware.Sensors)
                        {
                            if (sensor.Value.HasValue)
                            {
                                RecordSensorValue(sensor);
                            }
                        }
                    }

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.Value.HasValue)
                        {
                            RecordSensorValue(sensor);
                        }
                    }
                }

                Thread.Sleep(500);
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
        }
    }

    private static void RecordSensorValue(ISensor sensor)
    {
        if (sensor.SensorType == SensorType.Temperature)
        {
            if (sensor.Hardware.HardwareType == HardwareType.Cpu)
            {
                SendSensorData("CpuTemperature", sensor.Value.Value);
            }
            else if (HardwareIsGpu(sensor.Hardware.HardwareType))
            {
                if (!String.IsNullOrEmpty(sensor.Name) && sensor.Name.Contains("spot", StringComparison.OrdinalIgnoreCase))
                {
                    SendSensorData("GpuHotSpotTemperature", sensor.Value.Value);
                }
                else
                {
                    SendSensorData("GpuTemperature", sensor.Value.Value);
                }
            }
        }
        else if (sensor.SensorType == SensorType.Load)
        {
            if (sensor.Hardware.HardwareType == HardwareType.Cpu && !String.IsNullOrEmpty(sensor.Name) &&
                sensor.Name.Contains("total", StringComparison.OrdinalIgnoreCase))
            {
                SendSensorData("CpuLoad", sensor.Value.Value);
            }
        }
        else if (sensor.SensorType == SensorType.Data)
        {
            if (sensor.Hardware.HardwareType == HardwareType.Memory && !String.IsNullOrEmpty(sensor.Name) &&
                String.Equals(sensor.Name, "memory used", StringComparison.OrdinalIgnoreCase))
            {
                SendSensorData("MemoryUsage", sensor.Value.Value);
            }
        }
        else if (sensor.SensorType == SensorType.SmallData)
        {
            if (HardwareIsGpu(sensor.Hardware.HardwareType) && !String.IsNullOrEmpty(sensor.Name) &&
                String.Equals(sensor.Name, "gpu memory used", StringComparison.OrdinalIgnoreCase))
            {
                SendSensorData("GpuMemoryUsage", (sensor.Value.Value));
            }
        }
    }

    private static bool HardwareIsGpu(HardwareType hardwareType)
    {
        return hardwareType == HardwareType.GpuAmd ||
               hardwareType == HardwareType.GpuIntel ||
               hardwareType == HardwareType.GpuNvidia;
    }

    private static bool SendSensorData(string sensorId, float sensorValue)
    {
        bool success = false;

        if (_pipe?.Connection?.IsConnected == true)
        {
            if (!_pipe.MessageHandler.TryWrite(new Message(sensorId, sensorValue.ToString())))
            {
                Log("Sending Sensor Data Failed");
            }
        }

        return success;
    }

    private static void Log(string message)
    {
        Console.WriteLine(message);
        File.AppendAllText("sensors_log.txt", $"{DateTime.UtcNow} - {message}\n");
    }

    #endregion
}
