using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeuroSpectator.Services.BCI.Muse.Interop;
using Newtonsoft.Json;
using NeuroSpectator.Models.BCI.Muse;

namespace NeuroSpectator.Services.BCI.Muse.Core
{
    public class MuseManager
    {
        private const string MinApiVersion = "1.1";
        private const string MaxApiVersion = "1.1";
        private static readonly ApiCallback onMuseListChanged = OnMuseListChanged;
        private static readonly ApiCallback onReceiveLog = OnReceiveLog;
        private IMuseListener museListener;
        private ILogListener logListener;
        private static MuseManager instance;

        public static MuseManager GetInstance()
        {
            if (instance == null)
            {
                instance = new MuseManager();
                instance.RemoveFromListAfter(0);
            }
            return instance;
        }

        private MuseManager()
        {
            var version = Native.ApiVersion;
            var actual = Version.Parse(version);
            var min = Version.Parse(MinApiVersion);
            var max = Version.Parse(MaxApiVersion);
            if (actual < min || actual > max)
            {
                throw new ApiException($"Unsupported API version {version}, update libmuse.");
            }
            Native.Initialize();
        }

        [AOT.MonoPInvokeCallback(typeof(ApiCallback))]
        private async static void OnMuseListChanged(string _)
        {
            await Task.Run(() =>
            {
                GetInstance().museListener?.MuseListChanged();
            });
        }

        [AOT.MonoPInvokeCallback(typeof(ApiCallback))]
        private async static void OnReceiveLog(string json)
        {
            await Task.Run(() =>
            {
                GetInstance().logListener?.ReceiveLog(LogPacket.FromJson(json));
            });
        }

        public List<MuseInfo> GetMuses()
        {
            var json = Native.GetMuses();
            return JsonConvert.DeserializeObject<List<MuseInfo>>(json);
        }

        public void StartListening()
        {
            Native.StartListening();
        }

        public void StopListening()
        {
            Native.StopListening();
        }

        public void SetMuseListener(IMuseListener listener)
        {
            this.museListener = listener;
            Native.SetMuseListener(listener == null ? null : onMuseListChanged);
        }

        public void SetLogListener(ILogListener listener)
        {
            this.logListener = listener;
            Native.SetLogListener(listener is null ? null : onReceiveLog);
        }

        public void RemoveFromListAfter(long seconds)
        {
            Native.RemoveFromListAfter(seconds);
        }
    }
}