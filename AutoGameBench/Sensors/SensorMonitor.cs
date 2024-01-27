using System;
using System.Collections.Generic;
using System.Linq;
using AutoGameBench.IPC;
using CoreHook.IPC.Messages;
using CoreHook.IPC.NamedPipes;
using CoreHook.IPC.Transport;

namespace AutoGameBench.Sensors;

public sealed class SensorMonitor : IDisposable
{
    #region Fields

    private readonly INamedPipe _pipeServer;

    private bool _isMonitoring;
    private List<float> _cpuTemperatures;
    private List<float> _cpuLoads;
    private List<float> _memoryUsage;
    private List<float> _gpuTemperatures;
    private List<float> _gpuHotSpotTemperatures;
    private List<float> _gpuMemoryUsage;

    #endregion

    #region Constructor

    public SensorMonitor()
    {
        _pipeServer = NamedPipeServer.StartNewServer("AutoGameBenchSensorMonitor", new PipePlatformBase(), HandleSensorMessage);
    }

    #endregion

    #region Properties

    public float AverageCpuTemperature
    {
        get { return GetAverage(_cpuTemperatures); }
    }

    public float AverageCpuLoad
    {
        get { return GetAverage(_cpuLoads); }
    }

    public float AverageMemoryUsage
    {
        get { return GetAverage(_memoryUsage); }
    }

    public float AverageGpuTemperature
    {
        get { return GetAverage(_gpuTemperatures); }
    }

    public float AverageGpuHotSpotTemperature
    {
        get { return GetAverage(_gpuHotSpotTemperatures); }
    }

    public float AverageGpuMemoryUsage
    {
        get { return GetAverage(_gpuMemoryUsage); }
    }

    #endregion

    #region Public Methods

    public void Dispose()
    {
        _pipeServer.Dispose();
    }

    public void StartMonitoring()
    {
        if (!_isMonitoring && _pipeServer?.Connection?.IsConnected == true)
        {
            _cpuTemperatures = new List<float>();
            _cpuLoads = new List<float>();
            _memoryUsage = new List<float>();
            _gpuTemperatures = new List<float>();
            _gpuHotSpotTemperatures = new List<float>();
            _gpuMemoryUsage = new List<float>();

            _pipeServer.MessageHandler.TryWrite(new Message("Command", "Start"));
            _isMonitoring = true;
        }
    }

    public void StopMonitoring()
    {
        if (_isMonitoring && _pipeServer?.Connection?.IsConnected == true)
        {
            _pipeServer.MessageHandler.TryWrite(new Message("Command", "Stop"));
            _isMonitoring = false;
        }
    }

    #endregion

    #region Private Methods

    private void HandleSensorMessage(IMessage message, ITransportChannel transport)
    {
        if (_isMonitoring)
        {
            if (Single.TryParse(message.Body, out float sensorValue))
            {
                if (String.Equals(message.Header, "CpuTemperature", StringComparison.OrdinalIgnoreCase))
                {
                    _cpuTemperatures.Add(sensorValue);
                }
                else if (String.Equals(message.Header, "CpuLoad", StringComparison.OrdinalIgnoreCase))
                {
                    _cpuLoads.Add(sensorValue);
                }
                else if (String.Equals(message.Header, "MemoryUsage", StringComparison.OrdinalIgnoreCase))
                {
                    _memoryUsage.Add(sensorValue);
                }
                else if (String.Equals(message.Header, "GpuTemperature", StringComparison.OrdinalIgnoreCase))
                {
                    _gpuTemperatures.Add(sensorValue);
                }
                else if (String.Equals(message.Header, "GpuHotSpotTemperature", StringComparison.OrdinalIgnoreCase))
                {
                    _gpuHotSpotTemperatures.Add(sensorValue);
                }
                else if (String.Equals(message.Header, "GpuMemoryUsage", StringComparison.OrdinalIgnoreCase))
                {
                    _gpuMemoryUsage.Add(sensorValue);
                }
            }
        }
    }

    private float GetAverage(IEnumerable<float> collection)
    {
        float average = 0.0f;

        if (collection?.Any() == true)
        {
            average = collection.Average();
        }

        return average;
    }

    #endregion
}
