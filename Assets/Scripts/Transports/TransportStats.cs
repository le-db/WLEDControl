using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace WLEDControl
{
    /// <summary>
    /// Collects per-frame timings and logs one summary line per second, for comparing transports.
    /// </summary>
    public class TransportStats
    {
        const double ReportIntervalSeconds = 1;

        readonly string _transportName;
        readonly Stopwatch _window = Stopwatch.StartNew();

        int _frames;
        int _bytes;
        int _packets;
        double _readbackMsSum;
        double _sendMsSum;
        double _sendMsMax;

        public TransportStats(string transportName)
        {
            _transportName = transportName;
        }

        public void Record(double readbackMs, double sendMs, SendInfo info)
        {
            _frames++;
            _bytes += info.Bytes;
            _packets += info.Packets;
            _readbackMsSum += readbackMs;
            _sendMsSum += sendMs;
            if (sendMs > _sendMsMax) _sendMsMax = sendMs;

            if (_window.Elapsed.TotalSeconds >= ReportIntervalSeconds)
                ReportAndReset();
        }

        void ReportAndReset()
        {
            var seconds = _window.Elapsed.TotalSeconds;

            Debug.Log($"[{_transportName}] " +
                $"{_frames / seconds:F1} fps sent | " +
                $"send {_sendMsSum / _frames:F2}ms avg, {_sendMsMax:F2}ms max | " +
                $"readback {_readbackMsSum / _frames:F2}ms avg | " +
                $"{_bytes / seconds / 1024:F1} KiB/s | " +
                $"{(float)_packets / _frames:F1} packets/frame");

            _window.Restart();
            _frames = 0;
            _bytes = 0;
            _packets = 0;
            _readbackMsSum = 0;
            _sendMsSum = 0;
            _sendMsMax = 0;
        }
    }
}
