local vector2 = require("lstg.Vector2")
local cjson = require("cjson")

function GameInit()
	--print("Hi from core.lua :3")

	lstg.Window.SetTitle("Entropy test library")

	local music = lstg.LoadMusic("SA", "SA.wav", 0, 0)
	local music2 = lstg.LoadMusic("Adrienne", "Adrienne.ogg", 0, 0) --I own that song (technically)

	print(music)
	print(music2)

	music:Play(0.5)
	music2:Play(0.5)
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