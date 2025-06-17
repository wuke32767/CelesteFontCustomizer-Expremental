using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.FontCustomizer
{
    internal class RuntimeTexture : VirtualTexture
    {
        public RuntimeTexture(string name, int width, int height, Color color)
            : base(name, width, height, color)
        {
            Texture = new Texture2D(Engine.Graphics.GraphicsDevice, width, height);
        }

        public override void Dispose()
        {
            Texture.Dispose();
            Texture = null;
        }
        public override void Unload()
        {
        }
        public override void Reload()
        {
        }
    }
}
