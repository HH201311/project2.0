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
    }
}
