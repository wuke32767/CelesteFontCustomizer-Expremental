﻿using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Celeste.TextMenu;

namespace Celeste.Mod.FontCustomizer
{
    public static class EXT
    {
        public static TextMenuExt.EaseInSubHeaderExt AddDescriptionManually(this TextMenu.Item option, TextMenu containingMenu, string description)
        {
            TextMenuExt.EaseInSubHeaderExt descriptionText = new TextMenuExt.EaseInSubHeaderExt(description, initiallyVisible: false, containingMenu)
            {
                TextColor = Color.Gray,
                HeightExtra = 0f
            };
            List<TextMenu.Item> items = containingMenu.Items;
            if (items.Contains(option))
            {
                containingMenu.Insert(items.IndexOf(option) + 1, descriptionText);
            }

            //option.OnEnter = (Action)Delegate.Combine(option.OnEnter, (Action)delegate
            //{
            //    descriptionText.FadeVisible = true;
            //});
            //option.OnLeave = (Action)Delegate.Combine(option.OnLeave, (Action)delegate
            //{
            //    descriptionText.FadeVisible = false;
            //});
            return descriptionText;
        }
        public static TextMenu RemoveAt(this TextMenu self,int item)
        {
            int num = item;
            if (num == -1)
            {
                return self;
            }
            var iir = self.items[item];
            self.items.RemoveAt(num);
            iir.Container = null;
            self.Remove(iir.ValueWiggler);
            self.Remove(iir.SelectWiggler);
            if (!self.BatchMode)
            {
                self.RecalculateSize();
            }

            return self;
        }

    }
    public class PlaceHolder : Item
    {
    }
    public class SliderConfirmed : OptionConfirmed<int>
    {
        //
        // 摘要:
        //     Create a new Celeste.TextMenu.Slider.
        //
        // 参数:
        //   label:
        //     The display name for this setting.
        //
        //   values:
        //     A function for getting the display System.String for an System.Int32 value.
        //
        //   min:
        //     The minimum possible value.
        //
        //   max:
        //     The maximum possible value.
        //
        //   value:
        //     The initial value.
        public SliderConfirmed(string label, Func<int, string> values, int min, int max, int value = -1, string v = "USSRNAME_FontCustomizer_ShouldConfirmManually")
            : base(label, v)
        {
            for (int i = min; i <= max; i++)
            {
                Add(values(i), i, value == i);
            }
        }
    }


    public class OptionConfirmed<T> : Item
    {
        //
        // 摘要:
        //     The displayed name for this setting.
        public string Label;

        //
        // 摘要:
        //     The index of the current selection in Celeste.TextMenu.Option`1.Values.
        public int Index;
        private bool ModifiedNotSaved { get => Index != PreviousIndex; }

        //
        // 摘要:
        //     Invoked when the value changes.
        public Action<T> OnValueChange;
        public Action<T> OnValuePreview;

        //
        // 摘要:
        //     The previously selected index.
        public int PreviousIndex;

        //
        // 摘要:
        //     The list of label/value pairs.
        public List<(string, T)> Values;

        public string currentKey => Values[Index].Item1;
        public T currentValue => Values[Index].Item2;


        private float sine;

        private int lastDir;

        private float cachedRightWidth = 0f;

        private List<string> cachedRightWidthContent = [];

        //
        // 摘要:
        //     The color the text takes when the option is active, but unselected (defaults
        //     to white).
        public Color UnselectedColor = Color.White;

        //
        // 摘要:
        //     Create a new Celeste.TextMenu.Option`1.
        //
        // 参数:
        //   label:
        //     The display name for this setting.
        public OptionConfirmed(string label, string v = "USSRNAME_FontCustomizer_ShouldConfirmManually")
        {
            Values = [];
            //base._002Ector();
            Label = label;
            Selectable = true;

            OnLeave += () =>
            {
                Index = PreviousIndex;
            };
            subText = Dialog.Clean(v);
            OnEnter += ()=>OnValuePreview?.Invoke(currentValue);
        }
        public string subText;
        //
        // 摘要:
        //     Add an option.
        //
        // 参数:
        //   label:
        //     The display text for this option.
        //
        //   value:
        //     The T value of this option.
        //
        //   selected:
        //     Whether this option should start selected.
        public OptionConfirmed<T> Add(string label, T value, bool selected = false)
        {
            Values.Add((label, value));
            if (selected)
            {
                PreviousIndex = Index = Values.Count - 1;
            }

            return this;
        }

        //
        // 摘要:
        //     Set the action that will be invoked when the value changes.
        //
        // 参数:
        //   action:
        public OptionConfirmed<T> Change(Action<T> action)
        {
            OnValueChange = action;
            return this;
        }
        public OptionConfirmed<T> Preview(Action<T> action)
        {
            OnValuePreview = action;
            return this;
        }

        public override void Added()
        {
            Container.InnerContent = InnerContentMode.TwoColumn;
            if (subText != "")
            {
                this.AddDescription(Container, subText);
            }
        }

        public override void LeftPressed()
        {
            if (Index > 0)
            {
                Audio.Play("event:/ui/main/button_toggle_off");
                Index--;
                lastDir = -1;
                ValueWiggler.Start();

                OnValuePreview?.Invoke(currentValue);
            }
        }

        public override void RightPressed()
        {
            if (Index < Values.Count - 1)
            {
                Audio.Play("event:/ui/main/button_toggle_on");
                Index++;
                lastDir = 1;
                ValueWiggler.Start();

                OnValuePreview?.Invoke(currentValue);
            }
        }

        public override void ConfirmPressed()
        {
            OnValueChange?.Invoke(currentValue);
            PreviousIndex = Index;
        }
        public override void Update()
        {
            sine += Engine.RawDeltaTime;
        }

        public override float LeftWidth()
        {
            return ActiveFont.Measure(Label).X + 32f;
        }

        public override float RightWidth()
        {
            List<string> second = Values.Select(((string, T) val) => val.Item1).ToList();
            if (!cachedRightWidthContent.SequenceEqual(second))
            {
                cachedRightWidth = orig_RightWidth() * 0.8f + 44f;
                cachedRightWidthContent = second;
            }

            return cachedRightWidth;
        }

        public override float Height()
        {
            return ActiveFont.LineHeight;
        }

        public override void Render(Vector2 position, bool highlighted)
        {
            float alpha = Container.Alpha;
            Color strokeColor = Color.Black * (alpha * alpha * alpha);
            Color color = Disabled ? Color.DarkSlateGray : ((highlighted ? Container.HighlightColor : UnselectedColor) * alpha);
            ActiveFont.DrawOutline(Label, position, new Vector2(0f, 0.5f), Vector2.One, color, 2f, strokeColor);
            if (Values.Count > 0)
            {
                Color col = color;
                if (ModifiedNotSaved)
                {
                    col.G = (byte)(col.G / 7 * 3);
                    col.B = (byte)(col.B / 5 * 3);

                    //col.G /= 2;
                    //int i = col.B * 2;
                    //if(i>255)
                    //{
                    //    col.B = 255;
                    //}
                    //else
                    //{
                    //    col.B = (byte)i;
                    //}
                }
                float num = RightWidth();
                ActiveFont.DrawOutline(Values[Index].Item1, position + new Vector2(Container.Width - num * 0.5f + lastDir * ValueWiggler.Value * 8f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, col, 2f, strokeColor);
                Vector2 vector = Vector2.UnitX * (highlighted ? ((float)Math.Sin(sine * 4f) * 4f) : 0f);
                bool flag = Index > 0;
                Color color2 = flag ? color : (Color.DarkSlateGray * alpha);
                Vector2 position2 = position + new Vector2(Container.Width - num + 40f + ((lastDir < 0) ? ((0f - ValueWiggler.Value) * 8f) : 0f), 0f) - (flag ? vector : Vector2.Zero);
                ActiveFont.DrawOutline("<", position2, new Vector2(0.5f, 0.5f), Vector2.One, color2, 2f, strokeColor);
                bool flag2 = Index < Values.Count - 1;
                Color color3 = flag2 ? color : (Color.DarkSlateGray * alpha);
                Vector2 position3 = position + new Vector2(Container.Width - 40f + ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f), 0f) + (flag2 ? vector : Vector2.Zero);
                ActiveFont.DrawOutline(">", position3, new Vector2(0.5f, 0.5f), Vector2.One, color3, 2f, strokeColor);
            }
        }


        public float orig_RightWidth()
        {
            float num = 0f;
            foreach ((string, T) value in Values)
            {
                num = Math.Max(num, ActiveFont.Measure(value.Item1).X);
            }

            return num + 120f;
        }
    }

}
