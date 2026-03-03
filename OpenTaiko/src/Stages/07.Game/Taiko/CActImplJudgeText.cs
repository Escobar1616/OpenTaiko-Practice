using System.Drawing;
using FDK;

namespace OpenTaiko;

internal class CActImplJudgeText : CActivity {
	// コンストラクタ

	public CActImplJudgeText() {
		base.IsDeActivated = true;
	}

	public override void Activate() {
		JudgeAnimes = new List<JudgeAnime>[5];
		for (int i = 0; i < 5; i++) {
			JudgeAnimes[i] = new List<JudgeAnime>();
		}
		_timingFont = HPrivateFastFont.tInstantiateMainFont(20);
		base.Activate();
	}

	public override void DeActivate() {
		for (int i = 0; i < 5; i++) {
			for (int j = 0; j < JudgeAnimes[i].Count; j++) {
				JudgeAnimes[i][j]?.timingTexture?.Dispose();
				JudgeAnimes[i][j] = null;
			}
		}
		_timingFont?.Dispose();
		_timingFont = null;
		base.DeActivate();
	}

	// CActivity 実装（共通クラスからの差分のみ）
	public override int Draw() {
		if (!base.IsDeActivated) {
			for (int j = 0; j < 5; j++) {
				for (int i = 0; i < JudgeAnimes[j].Count; i++) {
					var judgeC = JudgeAnimes[j][i];
					if (judgeC.counter.CurrentValue == judgeC.counter.EndValue) {
						judgeC.timingTexture?.Dispose();
						JudgeAnimes[j].RemoveAt(i--);
						continue;
					}
					judgeC.counter.Tick();

					if (OpenTaiko.Tx.Judge != null) {
						float moveValue = CubicEaseOut(judgeC.counter.CurrentValue / 410.0f) - 1.0f;

						float baseX = 0;
						float baseY = 0;

						if (OpenTaiko.ConfigIni.nPlayerCount == 5) {
							baseX = OpenTaiko.Skin.Game_Judge_5P[0] + (OpenTaiko.Skin.Game_UIMove_5P[0] * j);
							baseY = OpenTaiko.Skin.Game_Judge_5P[1] + (OpenTaiko.Skin.Game_UIMove_5P[1] * j);
						} else if (OpenTaiko.ConfigIni.nPlayerCount == 4 || OpenTaiko.ConfigIni.nPlayerCount == 3) {
							baseX = OpenTaiko.Skin.Game_Judge_4P[0] + (OpenTaiko.Skin.Game_UIMove_4P[0] * j);
							baseY = OpenTaiko.Skin.Game_Judge_4P[1] + (OpenTaiko.Skin.Game_UIMove_4P[1] * j);
						} else {
							baseX = OpenTaiko.Skin.Game_Judge_X[j];
							baseY = OpenTaiko.Skin.Game_Judge_Y[j];
						}
						baseX += OpenTaiko.stageGameScreen.GetJPOSCROLLX(j);
						baseY += OpenTaiko.stageGameScreen.GetJPOSCROLLY(j);

						float x = baseX + (moveValue * OpenTaiko.Skin.Game_Judge_Move[0]);
						float y = baseY + (moveValue * OpenTaiko.Skin.Game_Judge_Move[1]);

						int opacity = (int)(255f - (judgeC.counter.CurrentValue >= 360 ? ((judgeC.counter.CurrentValue - 360) / 50.0f) * 255f : 0f));
						OpenTaiko.Tx.Judge.Opacity = opacity;
						OpenTaiko.Tx.Judge.t2D描画(x, y, judgeC.rc);

						// タイミングズレ表示（判定枠基準・右揃え・アニメーション無し）
						if (judgeC.timingTexture != null) {
							// GetNoteOriginX/Y が全プレイヤー数に対応した判定枠の座標を返す
							float frameX = OpenTaiko.stageGameScreen.GetNoteOriginX(j);
							float frameY = OpenTaiko.stageGameScreen.GetNoteOriginY(j);
							// 右揃え: 右端を (判定枠中心X + offset[0]) に固定
							float timingX = frameX + OpenTaiko.Skin.Game_Notes_Size[0] / 2.0f
								+ OpenTaiko.Skin.Game_TimingDisplay_Offset[0]
								- judgeC.timingTexture.szTextureSize.Width;
							// Y: 判定枠の下端 + offset[1]
							float timingY = frameY + OpenTaiko.Skin.Game_Notes_Size[1]
								+ OpenTaiko.Skin.Game_TimingDisplay_Offset[1];
							judgeC.timingTexture.Opacity = opacity;
							judgeC.timingTexture.t2D描画(timingX, timingY);
						}
					}
				}
			}
		}
		return 0;
	}

	public void Start(int player, ENoteJudge judge, int? msDelta = null) {
		JudgeAnime judgeAnime = new();
		judgeAnime.counter.Start(0, 410, 1, OpenTaiko.Timer);
		judgeAnime.Judge = judge;

		int njudge = 2;
		if (JudgesDict.ContainsKey(judge)) {
			njudge = JudgesDict[judge];
		}

		if (njudge == 0 && OpenTaiko.ConfigIni.SimpleMode) {
			return;
		}

		int height = OpenTaiko.Tx.Judge.szTextureSize.Height / 5;
		judgeAnime.rc = new Rectangle(0, (int)njudge * height, OpenTaiko.Tx.Judge.szTextureSize.Width, height);

		// タイミングズレテクスチャ生成
		if (msDelta.HasValue && OpenTaiko.ConfigIni.nTimingDisplayMode != 0 && _timingFont != null
			&& !(OpenTaiko.ConfigIni.bTimingDisplayOnlyImperfect && njudge == 0)) {
			// displayValue: 正 = FAST（早叩き）, 負 = SLOW（遅叩き）
			int displayValue = -msDelta.Value;
			bool isFast = displayValue > 0;
			bool isJust = displayValue == 0;

			Color textColor = isJust
				? Color.White
				: isFast
					? Color.FromArgb(0x3c, 0x94, 0xa3)   // FAST: #3c94a3
					: Color.FromArgb(0x90, 0x10, 0x2d);  // SLOW: #90102d

			string text = OpenTaiko.ConfigIni.nTimingDisplayMode switch {
				1 => isJust ? "" : (isFast ? "FAST" : "SLOW"),
				2 => (displayValue > 0 ? "+" : "") + (displayValue / 1000.0).ToString("0.000"),
				3 => (displayValue > 0 ? "+" : "") + displayValue + "ms",
				_ => ""
			};

			if (!string.IsNullOrEmpty(text)) {
				var bmp = _timingFont.DrawText(text, textColor, Color.Black, null, 8);
				judgeAnime.timingTexture = new CTexture(bmp);
				bmp.Dispose();
			}
		}

		// 既存アニメのタイミングテクスチャを破棄（判定アニメ自体はそのまま継続）
		foreach (var existing in JudgeAnimes[player]) {
			existing.timingTexture?.Dispose();
			existing.timingTexture = null;
		}

		JudgeAnimes[player].Add(judgeAnime);
	}

	// その他

	#region [ private ]
	//-----------------

	private static Dictionary<ENoteJudge, int> JudgesDict = new Dictionary<ENoteJudge, int> {
		[ENoteJudge.Perfect] = 0,
		[ENoteJudge.Auto] = 0,
		[ENoteJudge.Good] = 1,
		[ENoteJudge.Bad] = 2,
		[ENoteJudge.Miss] = 2,
		[ENoteJudge.ADLIB] = 3,
		[ENoteJudge.Mine] = 4,
	};

	private List<JudgeAnime>[] JudgeAnimes = new List<JudgeAnime>[5];
	private CCachedFontRenderer? _timingFont;

	private class JudgeAnime {
		public ENoteJudge Judge;
		public Rectangle rc;
		public CCounter counter = new CCounter();
		public CTexture? timingTexture;
	}

	private float CubicEaseOut(float p) {
		float f = (p - 1);
		return f * f * f + 1;
	}
	//-----------------
	#endregion
}
