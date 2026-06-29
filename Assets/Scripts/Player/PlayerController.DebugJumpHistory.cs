using System.Collections.Generic;
using UnityEngine;

public partial class PlayerController
{
    private const float DebugJumpHistorySamePositionToleranceSqr = 0.000001f;

    private readonly List<Vector2> debugJumpPositionHistory = new List<Vector2>();
    private int debugJumpHistoryCursor = -1;
    private bool debugHistoryCurrentPositionCaptured;

    private bool UpdateDebugJumpHistoryNavigation()
    {
        if (!IsDebugModeEnabled())
        {
            return false;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            StepDebugJumpHistory(-1);
            return true;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            StepDebugJumpHistory(1);
            return true;
        }

        return false;
    }

    private void StepDebugJumpHistory(int direction)
    {
        if (debugJumpPositionHistory.Count == 0)
        {
            return;
        }

        if (!debugHistoryCurrentPositionCaptured)
        {
            debugJumpPositionHistory.Add(GetCurrentPosition());
            debugJumpHistoryCursor = debugJumpPositionHistory.Count - 1;
            debugHistoryCurrentPositionCaptured = true;
        }

        int nextIndex = Mathf.Clamp(
            debugJumpHistoryCursor + direction,
            0,
            debugJumpPositionHistory.Count - 1);
        if (nextIndex == debugJumpHistoryCursor)
        {
            return;
        }

        debugJumpHistoryCursor = nextIndex;
        MoveToDebugJumpHistoryPosition(debugJumpPositionHistory[debugJumpHistoryCursor]);
    }

    private void SaveDebugJumpReturnPosition()
    {
        Vector2 currentPosition = GetCurrentPosition();
        if (debugHistoryCurrentPositionCaptured)
        {
            if (debugJumpHistoryCursor < debugJumpPositionHistory.Count - 1)
            {
                debugJumpPositionHistory.RemoveRange(
                    debugJumpHistoryCursor + 1,
                    debugJumpPositionHistory.Count - debugJumpHistoryCursor - 1);
            }

            if (debugJumpHistoryCursor >= 0
                && debugJumpHistoryCursor < debugJumpPositionHistory.Count
                && IsSameDebugJumpHistoryPosition(debugJumpPositionHistory[debugJumpHistoryCursor], currentPosition))
            {
                debugHistoryCurrentPositionCaptured = false;
                return;
            }
        }

        debugJumpPositionHistory.Add(currentPosition);
        debugJumpHistoryCursor = debugJumpPositionHistory.Count - 1;
        debugHistoryCurrentPositionCaptured = false;
    }

    private static bool IsSameDebugJumpHistoryPosition(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= DebugJumpHistorySamePositionToleranceSqr;
    }

    private void MoveToDebugJumpHistoryPosition(Vector2 position)
    {
        customJumpWindowOpen = false;
        powerGauge.Hide();
        angleAim.Hide();

        MovePlayerToPosition(position);
        if (groundChecker != null && groundChecker.CheckGroundedNow())
        {
            EnterIdle();
        }
        else
        {
            CancelPreparationAndWaitForLanding();
        }

        DebugJumpHistoryMoved?.Invoke(this);
    }
}
