using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Editor-only utilities for checking the prefab status of Components and GameObjects.
/// </summary>
public class PrefabCheckerUtility
{
    /// <summary>
    ///     Checks if <paramref name="component" /> is related to a prefab in any way:
    ///     part of a prefab asset on disk, part of a prefab instance in a scene,
    ///     or currently being edited in Prefab Mode.
    /// </summary>
    public static bool IsPartOfAnyPrefab(Component component)
    {
        return component != null && IsPartOfAnyPrefab(component.gameObject);
    }

    /// <summary>
    ///     Checks if <paramref name="gameObject" /> is related to a prefab in any way:
    ///     part of a prefab asset on disk, part of a prefab instance in a scene,
    ///     or currently being edited in Prefab Mode.
    /// </summary>
    public static bool IsPartOfAnyPrefab(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
        {
            return true;
        }

        return PrefabStageUtility.GetPrefabStage(gameObject) != null;
    }

    /// <summary>
    ///     Checks if <paramref name="component" /> lives inside a prefab asset
    ///     on disk (the .prefab file itself), and is NOT an instance of that prefab in a scene,
    ///     or an instance of that prefab nested in another prefab asset.
    /// </summary>
    public static bool IsPartOfPrefabAssetOnly(Component component)
    {
        if (component == null)
        {
            return false;
        }

        return IsPartOfPrefabAssetOnly(component.gameObject);
    }

    /// <summary>
    ///     Checks if <paramref name="gameObject" /> lives inside a prefab asset
    ///     on disk (the .prefab file itself), and is NOT an instance of that prefab in a scene,
    ///     or an instance of that prefab nested in another prefab asset.
    /// </summary>
    public static bool IsPartOfPrefabAssetOnly(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        // EditorUtility.IsPersistent catches all objects stored on disk, including
        // temporary import objects that IsPartOfPrefabAsset can miss during import.
        if (!EditorUtility.IsPersistent(gameObject))
        {
            return false;
        }

        if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            return false;
        }

        // Guard against nested prefab instances that live inside a persistent
        // prefab asset — those are technically "instances within an asset".
        return !PrefabUtility.IsPartOfPrefabInstance(gameObject);
    }

    /// <summary>
    ///     Checks if <paramref name="component" /> belongs to a valid
    ///     prefab instance that lives in a regular scene (not in Prefab Mode and not in
    ///     a preview scene used by inspectors/thumbnails/etc.).
    /// </summary>
    public static bool IsPartOfValidPrefabInstance(Component component)
    {
        if (component == null)
        {
            return false;
        }

        return IsPartOfValidPrefabInstance(component.gameObject);
    }

    /// <summary>
    ///     Checks if <paramref name="gameObject" /> belongs to a valid
    ///     prefab instance that lives in a regular scene (not in Prefab Mode and not in
    ///     a preview scene used by inspectors/thumbnails/etc.).
    /// </summary>
    public static bool IsPartOfValidPrefabInstance(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        // Must be a non-asset prefab instance — i.e. a scene instance only.
        // IsPartOfNonAssetPrefabInstance returns false for nested prefab instances
        // that live inside a persistent prefab asset on disk, which IsPartOfPrefabInstance would include.
        if (!PrefabUtility.IsPartOfNonAssetPrefabInstance(gameObject))
        {
            return false;
        }

        // The instance must be fully connected — not missing its asset, and not mid-import.
        PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(gameObject);
        if (status != PrefabInstanceStatus.Connected)
        {
            return false;
        }

        // Reject objects that are inside a prefab editing stage.
        if (PrefabStageUtility.GetPrefabStage(gameObject) != null)
        {
            return false;
        }

        // Reject objects that live in a preview scene (inspector previews, thumbnails, etc.).
        return !IsInPreviewScene(gameObject);
    }

    /// <summary>
    ///     Checks if <paramref name="component" /> lives in a prefab editing stage.
    /// </summary>
    public static bool IsInPrefabStage(Component component)
    {
        return component != null && IsInPrefabStage(component.gameObject);
    }

    /// <summary>
    ///     Checks if <paramref name="gameObject" /> lives in a prefab editing stage.
    /// </summary>
    public static bool IsInPrefabStage(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        // Reject objects that are inside a prefab editing stage.
        return PrefabStageUtility.GetPrefabStage(gameObject) != null;
    }

    /// <summary>
    ///     Checks if <paramref name="component" /> lives in a preview scene
    ///     (used by inspector previews, asset thumbnail cameras, etc.).
    /// </summary>
    public static bool IsInPreviewScene(Component component)
    {
        return component != null && IsInPreviewScene(component.gameObject);
    }

    /// <summary>
    ///     Checks if <paramref name="gameObject" /> lives in a preview scene
    ///     (used by inspector previews, asset thumbnail cameras, etc.).
    /// </summary>
    public static bool IsInPreviewScene(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        if (EditorSceneManager.IsPreviewSceneObject(gameObject))
        {
            return true;
        }

        Scene scene = gameObject.scene;
        return scene.IsValid() && EditorSceneManager.IsPreviewScene(scene);
    }
}