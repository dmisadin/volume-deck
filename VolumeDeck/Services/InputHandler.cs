using VolumeDeck.Models.Enums;

namespace VolumeDeck.Services;

public class InputHandler
{
    private readonly float VolumeStep = 0.02f;

    private readonly SessionVolumeController sessionVolumeController;
    
    public InputHandler(SessionVolumeController sessionVolumeController)
    {
        this.sessionVolumeController = sessionVolumeController;
    }

    public void HandleSerialLine(string line)
    {
        if (!int.TryParse(line, out int pin))
            return;

        if (!Enum.IsDefined(typeof(SerialVolumeControl), pin))
            return;

        switch ((SerialVolumeControl)pin)
        {
            case SerialVolumeControl.PreviousSession:
                this.sessionVolumeController.SessionNavigationByStep(-1);
                break;

            case SerialVolumeControl.NextSession:
                this.sessionVolumeController.SessionNavigationByStep(1);
                break;

            case SerialVolumeControl.VolumeDown:
                this.sessionVolumeController.AdjustSelectedVolume(-VolumeStep);
                break;

            case SerialVolumeControl.VolumeUp:
                this.sessionVolumeController.AdjustSelectedVolume(+VolumeStep);
                break;

            case SerialVolumeControl.MuteToggle:
                this.sessionVolumeController.ToggleMuteSession();
                break;
        }
    }
}
