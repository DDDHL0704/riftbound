extends SceneTree

# Convert the existing project SVG directly to a native Windows icon. Loading
# Image bytes avoids the exporter importing the SVG as a Texture2D twice.
func _init() -> void:
	var output := "res://assets/windows-icon.ico"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--output="):
			output = argument.trim_prefix("--output=")
	var source := Image.new()
	var loaded := source.load_svg_from_string(FileAccess.get_file_as_string("res://icon.svg"))
	if loaded != OK or source.is_empty():
		push_error("Project SVG could not be loaded for the Windows icon.")
		quit(1)
		return
	var sizes := [16, 32, 48, 64, 128, 256]
	var images: Array[PackedByteArray] = []
	for size in sizes:
		var resized: Image = source.duplicate()
		resized.resize(size, size, Image.INTERPOLATE_LANCZOS)
		images.append(resized.save_png_to_buffer())
	var file := FileAccess.open(output, FileAccess.WRITE)
	if file == null:
		push_error("Windows icon output could not be opened.")
		quit(1)
		return
	file.store_16(0)
	file.store_16(1)
	file.store_16(sizes.size())
	var offset := 6 + 16 * sizes.size()
	for index in sizes.size():
		var size: int = sizes[index] if sizes[index] < 256 else 0
		file.store_8(size)
		file.store_8(size)
		file.store_8(0)
		file.store_8(0)
		file.store_16(1)
		file.store_16(32)
		file.store_32(images[index].size())
		file.store_32(offset)
		offset += images[index].size()
	for bytes in images:
		file.store_buffer(bytes)
	file.close()
	print("WINDOWS_ICON_BUILT: six PNG sizes from the existing project SVG")
	quit()
