using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class RemoveScaleEditorWindow : EditorWindow
{
    private List<AnimationClip> clips = new();

    [MenuItem("Window/Animation/Remove Scale from Clips")]
    public static void ShowWindow()
    {
        GetWindow<RemoveScaleEditorWindow>("Remove Scale Curves");
    }

    private void OnGUI()
    {
        GUILayout.Label("Batch Remove Scale Curves", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag and drop AnimationClips or use the picker to batch remove scale (m_LocalScale) properties.", MessageType.Info);

        // Single object field (optional)
        EditorGUI.BeginChangeCheck();
        var pickedClip = EditorGUILayout.ObjectField("Add Animation Clip", null, typeof(AnimationClip), false) as AnimationClip;
        if (EditorGUI.EndChangeCheck() && pickedClip != null && !clips.Contains(pickedClip))
        {
            clips.Add(pickedClip);
        }

        HandleDragAndDrop();

        GUILayout.Space(10);

        if (clips.Count > 0)
        {
            GUILayout.Label($"Clips selected: {clips.Count}", EditorStyles.boldLabel);

            if (GUILayout.Button("Remove Scale From Selected Clips"))
            {
                RemoveScaleCurvesBatch(clips);
            }

            if (GUILayout.Button("Clear Selection"))
            {
                clips.Clear();
            }
        }
    }

    private void HandleDragAndDrop()
    {
        Event evt = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag & Drop Animation Clips Here", EditorStyles.helpBox);

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is AnimationClip clip && !clips.Contains(clip))
                        {
                            clips.Add(clip);
                        }
                    }
                    evt.Use();
                }
            }
        }
    }

    private void RemoveScaleCurvesBatch(List<AnimationClip> clips)
    {
        int totalRemoved = 0;
        int group = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Batch Remove Scale Curves");

        for (int i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            EditorUtility.DisplayProgressBar("Removing Scale", $"Processing {clip.name} ({i + 1}/{clips.Count})", i / (float)clips.Count);

            bool modified = false;
            Undo.RegisterCompleteObjectUndo(clip, "Remove Scale Curves");

            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.propertyName.Contains("m_LocalScale"))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        modified = true;
                        totalRemoved++;
                    }
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(clip);
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorUtility.ClearProgressBar();

        if (totalRemoved > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"✔ Finished: Removed {totalRemoved} scale curve(s) from {clips.Count} clip(s).");
        }
        else
        {
            Debug.Log($"ℹ No scale curves found in the selected clips.");
        }
    }
}
