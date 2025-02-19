using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpectatorMAUI.Services
{
    public enum ConnectionState
    {
        UNKNOWN = 0,
        CONNECTED = 1,
        CONNECTING = 2,
        DISCONNECTED = 3,
        NEEDS_UPDATE = 4,
        NEEDS_LICENSE = 5
    }

    public enum MuseDataPacketType
    {
        ACCELEROMETER = 0,
        GYRO = 1,
        EEG = 2,
        BATTERY = 3,
        DRL_REF = 4,
        ALPHA_ABSOLUTE = 5,
        BETA_ABSOLUTE = 6,
        DELTA_ABSOLUTE = 7,
        THETA_ABSOLUTE = 8,
        GAMMA_ABSOLUTE = 9,
    }
}
