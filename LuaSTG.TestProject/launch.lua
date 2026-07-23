_luastg_version = 0x1000
_luastg_min_support = 0x1000

setting = {}
if #lstg.args >= 2 then
	loadstring(lstg.args[2])()
end

lstg.FileManager.AddSearchPath("data")

for _,v in ipairs({"core","data","background","se","music","font"}) do
	local path = v .. ".zip"
	if lstg.FileManager.FileExist(path) then
		lstg.LoadPack(path)
	else
		path = "Library/" .. path
		if lstg.FileManager.FileExist(path) then
			lstg.LoadPack(path)
		end
	end
end

lstg.FileManager.CreateDirectory("mod")
local zip_path = string.format("mod/%s.zip", setting.mod)
local dir_path = string.format("mod/%s/", setting.mod)
local dir_root_script = string.format("mod/%s/root.lua", setting.mod)
if lstg.FileManager.FileExist(zip_path) then
	lstg.LoadPack(zip_path)
elseif lstg.FileManager.FileExist(dir_root_script) then
	lstg.FileManager.AddSearchPath(dir_path)
end

lstg.SetEntryScript("core.lua")

lstg.Window.SetSplash(true)
lstg.Window.SetTitle("Entropy test library")
lstg.Audio.SetMasterVolume(50 / 100)
lstg.Audio.SetBGMVolume(40 / 100)
lstg.Audio.SetSEVolume(30 / 100)