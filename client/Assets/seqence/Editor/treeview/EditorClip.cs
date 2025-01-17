using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Seqence;

namespace UnityEditor.Seqence
{
    [Flags]
    public enum ClipMode { None = 0, Left = 1, Right = 2, Loop = 4 }

    public enum DragMode { None, Drag, Left, Right }

    public partial class EditorClip
    {
        protected EditorTrack track;
        protected IClip clip;
        public Rect rect;
        public DragMode dragMode;
        public bool select;
        Event e;

        private ClipMode clipMode;

        private Dictionary<EventT, Action<EventData>> dic = new Dictionary<EventT, Action<EventData>>();

        protected void Regist(EventT t, Action<EventData> cb)
        {
            dic.Add(t, cb);
        }

        public void Init(EditorTrack tr, IClip c)
        {
            this.track = tr;
            this.clip = c;
            rect = Rect.zero;
            dragMode = DragMode.None;
            e = Event.current;
            clipMode = ClipMode.None;
            select = false;
            Regist(EventT.Select, OnSelect);
        }

        private void OnSelect(EventData data)
        {
            EventSelectData d = data as EventSelectData;
            select = d.select;
        }

        public void RecvEvent(EventData d)
        {
            if (dic.ContainsKey(d.e))
            {
                dic[d.e].Invoke(d);
            }
        }

        public void OnGUI()
        {
            rect = track.RenderRect;
            rect.x = SeqenceWindow.inst.TimeToPixel(clip.start);
            float y = SeqenceWindow.inst.TimeToPixel(clip.end);
            var timeRect = SeqenceWindow.inst.timeAreaRect;
            rect.x = Mathf.Max(rect.x, timeRect.x);
            y = Mathf.Min(y, timeRect.xMax);
            rect.width = y - rect.x;
            rect.height = rect.height - 2;
            if (rect.width < 0) rect.width = 0;

            var color = EditorGUIUtility.isProSkin ? Color.gray : Color.white;
            if (select) color = color - SeqenceStyle.addtiveClip;
            if (EditorGUIUtility.isProSkin)
            {
                EditorGUI.DrawRect(rect, color);
                EditorGUI.DrawOutline(rect, 1, Color.white);
            }
            else
            {
                EditorGUI.DrawRect(rect, color);
                EditorGUI.DrawOutline(rect, 1, Color.gray);
            }

            Rect left = rect;
            left.x = rect.x - Mathf.Min(10, rect.width / 4);
            left.x = Mathf.Max(left.x, timeRect.x);
            left.width = Mathf.Min(20, rect.width / 2);
            EditorGUIUtility.AddCursorRect(left, MouseCursor.SplitResizeLeftRight);
            Rect right = left;
            right.x = rect.x + rect.width - Mathf.Min(10, rect.width / 4);
            right.x = Mathf.Max(right.x, timeRect.x);
            EditorGUIUtility.AddCursorRect(right, MouseCursor.SplitResizeLeftRight);
            ProcessEvent(left, right);
            clipMode = track.CalcuteClipMode(clip, out var loop);
            if (loop > 1e-2) DrawLoops(loop);
            if ((clipMode & ClipMode.Left) > 0)
            {
                left.x = rect.x + 2;
                left.width = 10;
                left.y = rect.y + rect.height / 3;
                left.height = rect.height / 2;
                GUI.Label(left, SeqenceStyle.clipInContent, SeqenceStyle.keyframe);
            }
            if ((clipMode & ClipMode.Right) > 0)
            {
                right.x = rect.xMax - 8;
                right.width = 8;
                right.y = rect.y + rect.height / 3;
                right.height = rect.height / 2;
                GUI.Label(right, SeqenceStyle.clipOutContent, SeqenceStyle.keyframe);
            }
            MixProcessor();
        }

        public virtual void PostGUI()
        {
            EditorGUI.LabelField(rect, clip.Display, SeqenceStyle.fontClip);
        }

        protected virtual bool CheckChildSelect(Vector2 pos)
        {
            return true;
        }

        private void DrawLoops(float piexlDuration)
        {
            using (new GUIColorOverride(new Color(0, 0, 0, 0.08f)))
            {
                Rect tmp = rect;
                tmp.x = tmp.xMax - piexlDuration;
                tmp.width = rect.xMax - tmp.x;
                GUI.Label(tmp, GUIContent.none, SeqenceStyle.displayBackground);
            }
        }

        private void ProcessEvent(Rect left, Rect right)
        {
            Vector2 p = e.mousePosition;
            if (!track.track.locked)
                switch (e.type)
                {
                    case EventType.MouseDown:
                        if (left.Contains(p))
                        {
                            dragMode = DragMode.Left;
                        }
                        else if (right.Contains(p))
                        {
                            dragMode = DragMode.Right;
                        }
                        else if (rect.Contains(p))
                        {
                            dragMode = DragMode.Drag;
                            EventMgr.EmitAll(new EventSelectData() {select = false});
                            if (!select)
                            {
                                CheckChildSelect(p);
                            }
                        }
                        else
                        {
                            dragMode = DragMode.None;
                        }
                        break;
                    case EventType.MouseUp:
                        if (dragMode != DragMode.None)
                        {
                            track?.track?.SortClip();
                            track?.track?.RebuildMix();
                        }
                        dragMode = DragMode.None;
                        break;
                    case EventType.MouseDrag:
                    case EventType.ScrollWheel:
                        Drag(e);
                        break;
                }
        }

        private void Drag(Event e)
        {
            if (dragMode == DragMode.Left)
            {
                DragStart(e);
            }
            else if (dragMode == DragMode.Right)
            {
                DragEnd(e);
            }
            else if (dragMode == DragMode.Drag)
            {
                OnDrag(e);
            }
        }

       
        private bool IsInRange(IClip clip, float t)
        {
            if (clip != null)
            {
                return t < clip.end && t > clip.start;
            }
            return false;
        }

        private bool ValidRange(Rect r)
        {
            var timeRect = SeqenceWindow.inst.timeAreaRect;
            return r.x >= timeRect.x && r.xMax <= timeRect.xMax;
        }

   
        private void DragStart(Event e)
        {
            rect.x = SeqenceWindow.inst.TimeToPixel(clip.start);
            if (track.AllowClipDrag(DragMode.Left, e.delta.x, clip))
            {
                rect.x += e.delta.x;
                var start2 = SeqenceWindow.inst.PiexlToTime(rect.x);
                if (start2 >= 0 && start2 <= clip.end)
                {
                    clip.duration -= (start2 - clip.start);
                    clip.start = Mathf.Max(0, start2);
                    e.Use();
                }
            }
        }

        private void DragEnd(Event e)
        {
            rect.x = SeqenceWindow.inst.TimeToPixel(clip.end);
            if (track.AllowClipDrag(DragMode.Right, e.delta.x, clip))
            {
                rect.x += e.delta.x;
                var end = SeqenceWindow.inst.PiexlToTime(rect.x);
                if (end > clip.start)
                {
                    clip.duration += (end - clip.end);
                    e.Use();
                }
            }
        }

        protected virtual void OnDrag(Event e)
        {
            rect.x += e.delta.x;
            clip.start = SeqenceWindow.inst.PiexlToTime(rect.x);
            clip.start = Mathf.Max(0, clip.start);
            e.Use();
            SeqenceWindow.inst.seqence.RecalcuteDuration();
        }
    }
}
