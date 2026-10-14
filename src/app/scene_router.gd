extends Node
## Autoload "SceneRouter". The only place that knows scene paths. Screens ask for a route and hand the next
## screen one-shot params. No class_name: a global class must not share an autoload's name.

enum Route { LOGO, MAIN_MENU, SETTINGS, STAGE, HUB }

const ROUTES: Dictionary[Route, String] = {
	Route.LOGO: "res://src/app/logo.tscn",
	Route.MAIN_MENU: "res://src/meta/menu/main_menu.tscn",
	Route.SETTINGS: "res://src/meta/menu/settings_screen.tscn",
	Route.STAGE: "res://src/app/main.tscn",
	Route.HUB: "res://src/meta/hub/hub.tscn",
}

## Param keys screens agree on, so no screen hard-codes another's strings.
const PARAM_STAGE_DEF: StringName = &"stage_def"

var _params: Dictionary = {}


func path_for(route: Route) -> String:
	return ROUTES.get(route, "")


## Changes to the route's scene; `params` become readable once through `take_params()` by the next screen.
## Returns false (and leaves the current screen and params alone) for an unknown route or a failed change.
func go(route: Route, params: Dictionary = {}) -> bool:
	var path := path_for(route)
	if path == "":
		push_error("SceneRouter: unknown route %s" % str(route))
		return false
	var previous := _params
	_params = params
	var error := _change_scene(path)
	if error != OK:
		_params = previous
		push_error("SceneRouter: cannot change to '%s' (error %d)" % [path, error])
		return false
	return true


func take_params() -> Dictionary:
	var taken := _params
	_params = {}
	return taken


func _change_scene(path: String) -> Error:
	return get_tree().change_scene_to_file(path)
