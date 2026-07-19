local vector2 = require("lstg.Vector2")
local cjson = require("cjson")

function GameInit()
	--print("Hi from core.lua :3")
end

function FrameFunc()
	--print(lstg.GetFPS())

	return false
end

function RenderFunc()
end

function GameExit()
end

function FocusGainFunc()
	print("Focus Gain")
end

function FocusLoseFunc()
	print("Focus Lose")
end