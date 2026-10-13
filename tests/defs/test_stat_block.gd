extends TestCase


func test_overrides_list_max_health_before_health() -> void:
	var stats := StatBlock.new()
	stats.max_health = 250.0
	var keys := stats.to_overrides().keys()
	assert_eq(keys.find("max_health") < keys.find("health"), true, "max_health must be applied first")
	assert_eq(stats.to_overrides()["health"], 250.0)


func test_validate_rejects_non_positive_health() -> void:
	var stats := StatBlock.new()
	stats.max_health = 0.0
	assert_eq(stats.validate().size(), 1)
	assert_no_errors(StatBlock.new().validate(), "defaults")
