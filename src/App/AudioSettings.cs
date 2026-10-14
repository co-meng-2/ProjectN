using Godot;
using VampireSurvivor.Meta;

namespace VampireSurvivor.App;

/// <summary>Applies persisted settings to the audio server.</summary>
public static class AudioSettings
{
	/// <summary>Floor for the linear volume so 0 maps to a finite, inaudible level instead of -inf dB.</summary>
	public const float MinLinear = 0.0001f;

	public static void Apply(SettingsData settings)
	{
		int bus = AudioServer.GetBusIndex("Master");
		AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(settings.MasterVolume, MinLinear)));
	}
}
