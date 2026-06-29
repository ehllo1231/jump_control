using UnityEngine;

public partial class PlayerController
{
    private const string SavedPlayerPositionExistsKey = "JumpTiming.PlayerPosition.Exists";
    private const string SavedPlayerPositionXKey = "JumpTiming.PlayerPosition.X";
    private const string SavedPlayerPositionYKey = "JumpTiming.PlayerPosition.Y";

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveCurrentPlayerPosition();
        }
    }

    private void OnApplicationQuit()
    {
        SaveCurrentPlayerPosition();
    }

    private Vector2 GetCurrentPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private void SaveCurrentPlayerPosition()
    {
        if (!ShouldUsePersistentPlayerPosition())
        {
            return;
        }

        Vector2 currentPosition = GetCurrentPosition();
        if (!IsValidSavedPlayerPosition(currentPosition))
        {
            return;
        }

        PlayerPrefs.SetInt(SavedPlayerPositionExistsKey, 1);
        PlayerPrefs.SetFloat(SavedPlayerPositionXKey, currentPosition.x);
        PlayerPrefs.SetFloat(SavedPlayerPositionYKey, currentPosition.y);
        PlayerPrefs.Save();
    }

    private void RestoreSavedPlayerPosition()
    {
        if (!TryLoadSavedPlayerPosition(out Vector2 savedPosition))
        {
            return;
        }

        MovePlayerToPosition(savedPosition);
    }

    private void MovePlayerToPosition(Vector2 position)
    {
        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        else
        {
            Vector3 currentPosition = transform.position;
            transform.position = new Vector3(position.x, position.y, currentPosition.z);
        }

        Physics2D.SyncTransforms();
    }

    private static bool TryLoadSavedPlayerPosition(out Vector2 savedPosition)
    {
        savedPosition = default;
        if (PlayerPrefs.GetInt(SavedPlayerPositionExistsKey, 0) != 1)
        {
            return false;
        }

        savedPosition = new Vector2(
            PlayerPrefs.GetFloat(SavedPlayerPositionXKey),
            PlayerPrefs.GetFloat(SavedPlayerPositionYKey));
        return IsValidSavedPlayerPosition(savedPosition);
    }

    private static bool IsValidSavedPlayerPosition(Vector2 position)
    {
        return IsFinite(position.x) && IsFinite(position.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private bool ShouldUsePersistentPlayerPosition()
    {
        return !IsDebugModeEnabled();
    }
}
