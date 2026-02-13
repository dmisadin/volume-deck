using NAudio.CoreAudioApi;

namespace VolumeDeck.Models
{
    public class SessionItem
    {
        public string DisplayName = "";
        public SimpleAudioVolume SimpleAudioVolume = null!;
        public float Volume;
        public bool IsMuted;
    }
}
