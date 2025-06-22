using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.FontCustomizer
{
    internal class Guardian : Entity
    {
        public override void SceneEnd(Scene scene)
        {
            base.SceneEnd(scene);
            lock (FontCustomizerModule.Instance)
            {
                FontCustomizerModule.Instance.LockededMerge();
            }

        }
        public override void Removed(Scene scene)
        {
            base.Removed(scene);
            lock (FontCustomizerModule.Instance)
            {
                FontCustomizerModule.Instance.LockededMerge();
            }
        }
    }
}
