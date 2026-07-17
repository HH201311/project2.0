using SharpHook;
using SharpHook.Native;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class MouseSimulationHelper
    {
        public static void MouseClick(EventSimulator simulator, MouseButton mouseButton, short x, short y)
        {
            simulator.SimulateMousePress(x, y, mouseButton);
            simulator.SimulateMouseRelease(x, y, mouseButton);
        }

        public static void MouseClick(EventSimulator simulator, MouseButton mouseButton)
        {
            simulator.SimulateMousePress(mouseButton);
            simulator.SimulateMouseRelease(mouseButton);
        }

        public static void MouseMove(EventSimulator simulator, short x, short y)
        {
            simulator.SimulateMouseMovement(x, y);
        }

        public static void MouseMoveRelative(EventSimulator simulator, short dx, short dy)
        {
            simulator.SimulateMouseMovementRelative(dx, dy);
        }

        public static void MouseDrag(EventSimulator simulator, MouseButton mouseButton, short startX, short startY, short endX, short endY)
        {
            for (short i = 0; i < endX - startX; i++)
            {
                simulator.SimulateMousePress((short)(startX + i), startY, mouseButton);
                Thread.Sleep(10);
            }
            simulator.SimulateMouseRelease(endX, endY, mouseButton);
        }

        public static void MouseDrag(EventSimulator simulator, MouseButton mouseButton, List<(short x, short y)> pathPoints)
        {
            foreach (var point in pathPoints)
            {
                simulator.SimulateMousePress(point.x, point.y, mouseButton);
                Thread.Sleep(10);
            }
            var lastPoint = pathPoints.Last();
            simulator.SimulateMouseRelease(lastPoint.x, lastPoint.y, mouseButton);
        }

        /// <summary>
        /// 标准拖拽：移到起点 → 按下 → 沿路径移动 → 在终点释放。
        /// </summary>
        public static void MouseDragPressMoveRelease(
            EventSimulator simulator,
            MouseButton mouseButton,
            IReadOnlyList<(short x, short y)> pathPoints,
            int stepDelayMs = 8)
        {
            if (pathPoints == null || pathPoints.Count < 2)
            {
                throw new ArgumentException("拖拽路径至少需要起点与终点", nameof(pathPoints));
            }

            var start = pathPoints[0];
            simulator.SimulateMouseMovement(start.x, start.y);
            Thread.Sleep(Math.Max(1, stepDelayMs));
            simulator.SimulateMousePress(mouseButton);

            for (var i = 1; i < pathPoints.Count; i++)
            {
                var p = pathPoints[i];
                simulator.SimulateMouseMovement(p.x, p.y);
                Thread.Sleep(Math.Max(1, stepDelayMs));
            }

            simulator.SimulateMouseRelease(mouseButton);
        }
    }
}
