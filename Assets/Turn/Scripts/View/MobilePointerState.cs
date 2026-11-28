using System;

namespace Turn.View
{
    /// <summary>Touch ownership and one-shot roll buffering. Pure state; does not mutate simulation.</summary>
    public sealed class MobilePointerState
    {
        private bool _stickHeld, _rollHeld, _rollQueued;
        private int _stickPointer, _rollPointer;
        public float X { get; private set; }
        public float Y { get; private set; }

        public bool BeginStick(int pointer)
        {
            if (_stickHeld) return false;
            _stickHeld = true;
            _stickPointer = pointer;
            return true;
        }

        public bool DragStick(int pointer, float x, float y, float deadzone)
        {
            if (!_stickHeld || pointer != _stickPointer) return false;
            float length = (float)Math.Sqrt(x * x + y * y);
            if (length > 1f) { x /= length; y /= length; length = 1f; }
            deadzone = Math.Max(0f, Math.Min(.95f, deadzone));
            if (length <= deadzone) X = Y = 0f;
            else
            {
                float remapped = (length - deadzone) / (1f - deadzone);
                X = x / length * remapped;
                Y = y / length * remapped;
            }
            return true;
        }

        public bool EndStick(int pointer)
        {
            if (!_stickHeld || pointer != _stickPointer) return false;
            _stickHeld = false;
            X = Y = 0f;
            return true;
        }

        public bool BeginRoll(int pointer, bool ready)
        {
            if (_rollHeld) return false;
            _rollHeld = true;
            _rollPointer = pointer;
            if (ready) _rollQueued = true;
            return ready;
        }

        public void EndRoll(int pointer)
        {
            if (_rollHeld && pointer == _rollPointer) _rollHeld = false;
        }

        public bool ConsumeRoll()
        {
            bool queued = _rollQueued;
            _rollQueued = false;
            return queued;
        }

        public void Reset()
        {
            _stickHeld = _rollHeld = _rollQueued = false;
            X = Y = 0f;
        }
    }
}
