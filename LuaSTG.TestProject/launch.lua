_luastg_version = 0x1000
_luastg_min_support = 0x1000

setting = {}
if #lstg.args >= 2 then
	loadstring(lstg.args[2])()
end

lstg.FileManager.AddSearchPath("data")