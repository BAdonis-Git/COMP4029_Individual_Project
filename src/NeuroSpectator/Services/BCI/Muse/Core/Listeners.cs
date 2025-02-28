namespace NeuroSpectator.Services.BCI.Muse.Core
{
    public interface IMuseConnectionListener
    {
        void ReceiveMuseConnectionPacket(MuseConnectionPacket packet, Muse muse);
    }

    public interface IMuseDataListener
    {
        void ReceiveMuseDataPacket(MuseDataPacket packet, Muse muse);
        void ReceiveMuseArtifactPacket(MuseArtifactPacket packet, Muse muse);
    }

    public interface IMuseErrorListener
    {
        void ReceiveError(MuseError packet, Muse muse);
    }

    public interface ILogListener
    {
        void ReceiveLog(LogPacket packet);
    }

    public interface IMuseListener
    {
        void MuseListChanged();
    }
}