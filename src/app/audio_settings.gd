class_name AudioSettings
extends RefCounted
## Applies persisted settings to the audio server.

## Floor for the linear volume so 0 maps to a finite, inaudible level instead of -inf dB.
const MIN_LINEAR: float = 0.0001


static func apply(settings: SettingsData) -> void:
	var bus := AudioServer.get_bus_index(&"Master")
	AudioServer.set_bus_volume_db(bus, linear_to_db(maxf(settings.master_volume, MIN_LINEAR)))
