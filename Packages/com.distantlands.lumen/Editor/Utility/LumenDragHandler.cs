// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Handles drag-and-drop operations for Lumen effect layers in the Unity Editor.

using UnityEditor;
using UnityEngine;

namespace DistantLands.Lumen.EditorScripts
{
    static class DragDropHandler
    {
        [InitializeOnLoadMethod]
        static void OnLoad()
        {
#if UNITY_6000_4_OR_NEWER
            DragAndDrop.AddDropHandlerV2(OnHierarchyDrop);
#else
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
#endif
        }

#if UNITY_6000_4_OR_NEWER
        private static DragAndDropVisualMode OnHierarchyDrop(EntityID dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (perform)
            {
                if (DragAndDrop.objectReferences[0] is LumenEffectProfile)
                {
                    LumenEffectProfile lumenEffect = (LumenEffectProfile)DragAndDrop.objectReferences[0];

                    GameObject go = new GameObject(lumenEffect.name);
                    LumenEffectPlayer player = go.AddComponent<LumenEffectPlayer>();
                    player.profile = lumenEffect;

                    GameObject parentObject = (GameObject)EditorUtility.EntityIDToObject(dropTargetInstanceID);
                    if (parentObject)
                    {
                        go.transform.SetParent(parentObject.transform);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                    }

                    Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                    Selection.activeObject = go;
                }
                else
                {
                    return DragAndDropVisualMode.None;
                }
            }

            return DragAndDropVisualMode.Move;
        }
#else
        private static DragAndDropVisualMode OnHierarchyDrop(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (perform)
            {
                if (DragAndDrop.objectReferences[0] is LumenEffectProfile)
                {
                    LumenEffectProfile lumenEffect = (LumenEffectProfile)DragAndDrop.objectReferences[0];

                    GameObject go = new GameObject(lumenEffect.name);
                    LumenEffectPlayer player = go.AddComponent<LumenEffectPlayer>();
                    player.profile = lumenEffect;

                    GameObject parentObject = (GameObject)EditorUtility.InstanceIDToObject(dropTargetInstanceID);
                    if (parentObject)
                    {
                        go.transform.SetParent(parentObject.transform);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                    }

                    Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                    Selection.activeObject = go;
                }
                else
                {
                    return DragAndDropVisualMode.None;
                }
            }

            return DragAndDropVisualMode.Move;
        }
#endif
    }
}