using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using System;

namespace IngameScript
{
    partial class Program
    {

        class ManagedDisplay
        {
            private IMyTextSurface surface;
            private RectangleF viewport;
            private MySpriteDrawFrame frame;
            private float StartHeight = 0f;
            private float HeadingHeight = 35f;
            private float LineHeight = 30f;
            private float HeadingFontSize = 1.3f;
            private float RegularFontSize = 1.0f;
            private Vector2 Position;
            private int WindowSize;         // Number of lines shown on screen at once after heading
            private Color HighlightColor;
            private int linesToSkip;
            private bool monospace;
            private readonly String SpritePrefix = "MyObjectBuilder_Component/";
            private bool MakeSpriteCacheDirty = false;
            private Color BackgroundColor, ForegroundColor;

            public ManagedDisplay(IMyTextSurface surface, float scale = 1.0f, Color highlightColor = new Color(), int linesToSkip = 0, bool monospace = false)
            {
                this.surface = surface;
                this.HighlightColor = highlightColor;
                this.linesToSkip = linesToSkip;
                this.monospace = monospace;
                this.BackgroundColor = surface.ScriptBackgroundColor;
                this.ForegroundColor = surface.ScriptForegroundColor;

                // Scale everything!
                StartHeight *= scale;
                HeadingHeight *= scale;
                LineHeight *= scale;
                HeadingFontSize *= scale;
                RegularFontSize *= scale;

                surface.ContentType = ContentType.SCRIPT;
                surface.Script = "TSS_FactionIcon";
                Vector2 padding = surface.TextureSize * (surface.TextPadding / 100);
                viewport = new RectangleF((surface.TextureSize - surface.SurfaceSize) / 2f + padding, surface.SurfaceSize - (2*padding));
                WindowSize = ((int)((viewport.Height - 10 * scale) / LineHeight));
            }

            private void AddHeading()
            {
                if (surface.Script != "")
                {
                    surface.Script = "";
                    surface.ScriptBackgroundColor = BackgroundColor;
                    surface.ScriptForegroundColor = ForegroundColor;
                }
                Position = new Vector2(viewport.Width / 10f, StartHeight) + viewport.Position;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "Textures\\FactionLogo\\Builders\\BuilderIcon_16.dds",
                    Position = Position + new Vector2(0f,LineHeight/2f),
                    Size = new Vector2(LineHeight,LineHeight),
                    RotationOrScale = HeadingFontSize,
                    Color = HighlightColor,
                    Alignment = TextAlignment.CENTER
                });
                Position.X += viewport.Width / 4f;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = "Stock",
                    Position = Position,
                    RotationOrScale = HeadingFontSize,
                    Color = HighlightColor,
                    Alignment = TextAlignment.RIGHT,
                    FontId = "White"
                });
                Position.X += viewport.Width / 4f;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = "Prod",
                    Position = Position,
                    RotationOrScale = HeadingFontSize,
                    Color = HighlightColor,
                    Alignment = TextAlignment.RIGHT,
                    FontId = "White"
                });
                Position.X += viewport.Width / 4f;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = "Req",
                    Position = Position,
                    RotationOrScale = HeadingFontSize,
                    Color = HighlightColor,
                    Alignment = TextAlignment.RIGHT,
                    FontId = "White"
                });
                Position.Y += HeadingHeight;
            }
            private void RenderRow(String SpriteName, Program.Requirement requirement)
            {
                Color IconColor;
                Color TextColor;
                if (requirement.Stock == 0)
                    IconColor = Color.Brown;
                else
                    IconColor = Color.Gray;
                if (requirement.Stock < requirement.Required)
                    if (requirement.Stock == 0)
                        TextColor = Color.Brown;
                    else
                        TextColor = Color.Goldenrod;
                else
                    TextColor = surface.ScriptForegroundColor;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = SpritePrefix+SpriteName,
                    Position = Position + new Vector2(0f,LineHeight/2f),
                    Size = new Vector2(LineHeight,LineHeight),
                    RotationOrScale = 0,
                    Color = IconColor,
                    Alignment = TextAlignment.CENTER,
                });
                Position.X += viewport.Width /4f;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = requirement.Stock.ToString(),
                    Position = Position,
                    RotationOrScale = RegularFontSize,
                    Color = TextColor,
                    Alignment = TextAlignment.RIGHT,
                    FontId = monospace?"Monospace":"White"
                });
                Position.X += viewport.Width / 4f;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = requirement.Production.ToString(),
                    Position = Position,
                    RotationOrScale = RegularFontSize,
                    Color = TextColor,
                    Alignment = TextAlignment.RIGHT,
                    FontId = monospace?"Monospace":"White"
                });
                Position.X += viewport.Width / 4f;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = requirement.Required.ToString(),
                    Position = Position,
                    RotationOrScale = RegularFontSize,
                    Color = TextColor,
                    Alignment = TextAlignment.RIGHT,
                    FontId = monospace?"Monospace":"White"
                });
            }

            internal void Render(Dictionary<String, Program.Requirement> Components)
            {
                MakeSpriteCacheDirty = !MakeSpriteCacheDirty;
                frame = surface.DrawFrame();
                if (MakeSpriteCacheDirty)
                {
                    frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Color = surface.BackgroundColor,
                        Position = new Vector2(0, 0),
                        Size = new Vector2(0, 0)
                    });
                }
                AddHeading();
                int renderLineCount = 0;
                foreach (var component in Components.Keys)
                {
                    if (++renderLineCount > linesToSkip)
                    {
                        Position.X = viewport.Width / 10f + viewport.Position.X;
                        if (renderLineCount >= linesToSkip && renderLineCount < linesToSkip + WindowSize)
                            RenderRow(component, Components[component]);
                        Position.Y += LineHeight;
                    }
                }
                frame.Dispose();
            }
        }
    }
}
