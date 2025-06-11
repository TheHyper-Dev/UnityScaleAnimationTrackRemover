using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System;

public class AnimationScaleTrackRemover
{
    private static readonly StringBuilder s_stringBuilder = new StringBuilder(256);
    
    [MenuItem("Assets/Remove Scale Tracks", false, 1000)]
    private static void RemoveScaleTracks()
    {
        // Get selected animation clips
        var selectedAnimations = GetSelectedAnimationClips();
        
        if (selectedAnimations.Count == 0)
        {
            EditorUtility.DisplayDialog("No Animation Clips Selected", 
                "Please select one or more .anim files to remove scale tracks from.", "OK");
            return;
        }

        // Confirm action
        string message = selectedAnimations.Count == 1 
            ? BuildSingleClipMessage(selectedAnimations[0].name)
            : BuildMultipleClipMessage(selectedAnimations.Count);
            
        if (!EditorUtility.DisplayDialog("Remove Scale Tracks", message, "Remove", "Cancel"))
            return;

        // Convert to array for Undo (avoiding ToArray() LINQ method)
        var clipsArray = new AnimationClip[selectedAnimations.Count];
        for (int i = 0; i < selectedAnimations.Count; i++)
        {
            clipsArray[i] = selectedAnimations[i];
        }

        // Record undo state for all selected animations
        Undo.RecordObjects(clipsArray, "Remove Scale Tracks");

        int processedCount = 0;
        for (int i = 0; i < selectedAnimations.Count; i++)
        {
            if (RemoveScaleTracksFromClip(selectedAnimations[i]))
                processedCount++;
        }

        // Mark assets as dirty and save
        for (int i = 0; i < selectedAnimations.Count; i++)
        {
            EditorUtility.SetDirty(selectedAnimations[i]);
        }
        AssetDatabase.SaveAssets();

        // Show result
        string resultMessage = BuildResultMessage(processedCount, selectedAnimations.Count);
        Debug.Log($"[AnimationScaleTrackRemover] {resultMessage}");
        
        if (processedCount < selectedAnimations.Count)
        {
            s_stringBuilder.Clear();
            s_stringBuilder.Append(resultMessage);
            s_stringBuilder.Append("\n\nSome clips may not have had scale tracks to remove.");
            
            EditorUtility.DisplayDialog("Partial Success", s_stringBuilder.ToString(), "OK");
        }
    }

    [MenuItem("Assets/Remove Scale Tracks", true)]
    private static bool ValidateRemoveScaleTracks()
    {
        return GetSelectedAnimationClips().Count > 0;
    }

    private static List<AnimationClip> GetSelectedAnimationClips()
    {
        var clips = new List<AnimationClip>();
        var seenClips = new HashSet<AnimationClip>();
        
        var selectedObjects = Selection.objects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            var obj = selectedObjects[i];
            AnimationClip clip = null;
            
            if (obj is AnimationClip directClip)
            {
                clip = directClip;
            }
            else
            {
                // Check if it's an asset that contains an AnimationClip
                string path = AssetDatabase.GetAssetPath(obj);
                if (EndsWithAnim(path))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                }
            }
            
            if (clip != null && seenClips.Add(clip))
            {
                clips.Add(clip);
            }
        }
        
        return clips;
    }

    private static bool RemoveScaleTracksFromClip(AnimationClip clip)
    {
        if (clip == null)
            return false;

        // Get all curve bindings
        var curveBindings = AnimationUtility.GetCurveBindings(clip);
        var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        
        bool hasScaleTracks = false;
        var scaleBindingsToRemove = new List<EditorCurveBinding>();

        // Find scale-related curve bindings
        for (int i = 0; i < curveBindings.Length; i++)
        {
            ref readonly var binding = ref curveBindings[i];
            if (IsScaleProperty(binding.propertyName.AsSpan()))
            {
                scaleBindingsToRemove.Add(binding);
                hasScaleTracks = true;
            }
        }

        // Remove scale tracks
        for (int i = 0; i < scaleBindingsToRemove.Count; i++)
        {
            AnimationUtility.SetEditorCurve(clip, scaleBindingsToRemove[i], null);
        }

        // Also check for scale-related object reference curves
        var scaleObjectBindingsToRemove = new List<EditorCurveBinding>();
        for (int i = 0; i < objectBindings.Length; i++)
        {
            ref readonly var binding = ref objectBindings[i];
            if (IsScaleProperty(binding.propertyName.AsSpan()))
            {
                scaleObjectBindingsToRemove.Add(binding);
                hasScaleTracks = true;
            }
        }

        for (int i = 0; i < scaleObjectBindingsToRemove.Count; i++)
        {
            AnimationUtility.SetObjectReferenceCurve(clip, scaleObjectBindingsToRemove[i], null);
        }

        return hasScaleTracks;
    }

    private static bool IsScaleProperty(ReadOnlySpan<char> propertyName)
    {
        // Check common scale properties using Span for efficient string operations
        return propertyName.StartsWith("m_LocalScale.".AsSpan()) ||
               propertyName.SequenceEqual("m_LocalScale".AsSpan()) ||
               propertyName.StartsWith("localScale.".AsSpan()) ||
               propertyName.SequenceEqual("localScale".AsSpan()) ||
               ContainsScalePattern(propertyName);
    }

    private static bool ContainsScalePattern(ReadOnlySpan<char> propertyName)
    {
        // Check for Scale.x/y/z patterns
        int scaleIndex = IndexOfSpan(propertyName, "Scale.".AsSpan());
        if (scaleIndex >= 0)
        {
            int dotIndex = scaleIndex + 6; // "Scale.".Length
            if (dotIndex < propertyName.Length)
            {
                char axis = propertyName[dotIndex];
                if (axis == 'x' || axis == 'y' || axis == 'z')
                    return true;
            }
        }

        // Check for .localScale.x/y/z patterns
        int localScaleIndex = IndexOfSpan(propertyName, ".localScale.".AsSpan());
        if (localScaleIndex >= 0)
        {
            int axisIndex = localScaleIndex + 12; // ".localScale.".Length
            if (axisIndex < propertyName.Length)
            {
                char axis = propertyName[axisIndex];
                if (axis == 'x' || axis == 'y' || axis == 'z')
                    return true;
            }
        }

        return false;
    }

    private static int IndexOfSpan(ReadOnlySpan<char> source, ReadOnlySpan<char> value)
    {
        if (value.Length == 0) return 0;
        if (value.Length > source.Length) return -1;

        for (int i = 0; i <= source.Length - value.Length; i++)
        {
            if (source.Slice(i, value.Length).SequenceEqual(value))
                return i;
        }
        return -1;
    }

    private static bool EndsWithAnim(string path)
    {
        if (path.Length < 5) return false;
        
        var span = path.AsSpan();
        return span[span.Length - 5] == '.' &&
               span[span.Length - 4] == 'a' &&
               span[span.Length - 3] == 'n' &&
               span[span.Length - 2] == 'i' &&
               span[span.Length - 1] == 'm';
    }

    private static string BuildSingleClipMessage(string clipName)
    {
        s_stringBuilder.Clear();
        s_stringBuilder.Append("Remove scale tracks from '");
        s_stringBuilder.Append(clipName);
        s_stringBuilder.Append("'?");
        return s_stringBuilder.ToString();
    }

    private static string BuildMultipleClipMessage(int count)
    {
        s_stringBuilder.Clear();
        s_stringBuilder.Append("Remove scale tracks from ");
        s_stringBuilder.Append(count);
        s_stringBuilder.Append(" animation clips?");
        return s_stringBuilder.ToString();
    }

    private static string BuildResultMessage(int processedCount, int totalCount)
    {
        s_stringBuilder.Clear();
        
        if (processedCount == totalCount)
        {
            s_stringBuilder.Append("Successfully removed scale tracks from ");
            s_stringBuilder.Append(processedCount);
            s_stringBuilder.Append(" animation clip");
            if (processedCount != 1) s_stringBuilder.Append('s');
            s_stringBuilder.Append('.');
        }
        else
        {
            s_stringBuilder.Append("Removed scale tracks from ");
            s_stringBuilder.Append(processedCount);
            s_stringBuilder.Append(" out of ");
            s_stringBuilder.Append(totalCount);
            s_stringBuilder.Append(" animation clip");
            if (totalCount != 1) s_stringBuilder.Append('s');
            s_stringBuilder.Append('.');
        }
        
        return s_stringBuilder.ToString();
    }
}
