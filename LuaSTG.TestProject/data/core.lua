local vector2 = require("lstg.Vector2")

function GameInit()
	print("Hi from core.lua :3")

	local v = vector2.create()
	v.x = 5
	v.x = v.x + 10

	print(v.x)

	--lstg.LoadMusic("SA", "SA.wav", 0, 0)
	--lstg.PlayMusic("SA")

	--lstg.LoadMusic("Himemiko", "Himemiko.ogg", 0, 0)
	--lstg.PlayMusic("Himemiko")

	--lstg.SetBGMVolume(0.5)

	local col = lstg.Color(64, 128, 255, 0)
	print(col)
end

function FrameFunc()
	print(lstg.GetFPS())
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