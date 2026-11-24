using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        #region Automated Methods
        private static void OverrideEditor(Type componentType, Type editorType)
        {
            Type attributeType = Type.GetType("UnityEditor.CustomEditorAttributes, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Type monoEditorType = Type.GetType("UnityEditor.CustomEditorAttributes+MonoEditorType, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
#if UNITY_6000_0_OR_NEWER
            // Unity 6 moved the editor registry into a lazy instance cache and made entries structs.
            const BindingFlags instanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

            var instance = attributeType.GetProperty("instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
                ?? throw new MissingMemberException("CustomEditorAttributes.instance");
            var cache = attributeType.GetField("m_Cache", instanceNonPublic)?.GetValue(instance)
                ?? throw new MissingMemberException("CustomEditorAttributes.m_Cache");
            var editorDictionary = cache.GetType().GetField("m_CustomEditorCache", instanceNonPublic)?.GetValue(cache) as IDictionary
                ?? throw new MissingMemberException("CustomEditorCache.m_CustomEditorCache");

            if (!editorDictionary.Contains(componentType))
                throw new InvalidOperationException($"No custom editor registered for {componentType.Name}");

            var storage = editorDictionary[componentType];
            var editorsList = (IList)storage.GetType().GetField("customEditors").GetValue(storage);

            var rpField = monoEditorType.GetField("supportedRenderPipelineTypes");
            var childField = monoEditorType.GetField("editorForChildClasses");
            var fallbackField = monoEditorType.GetField("isFallback");

            int index = -1;
            for (int i = 0; i < editorsList.Count; i++)
            {
                if (!(bool)fallbackField.GetValue(editorsList[i]))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
                throw new InvalidOperationException($"No non-fallback editor found for {componentType.Name}");

            var previous = editorsList[index];
            editorsList[index] = Activator.CreateInstance(monoEditorType, editorType,
                rpField.GetValue(previous), childField.GetValue(previous), false);
#else
            var editorsField = attributeType.GetField("kSCustomEditors", BindingFlags.Static | BindingFlags.NonPublic);
            var inspectorField = monoEditorType.GetField("m_InspectorType", BindingFlags.Public | BindingFlags.Instance);
            var editorDictionary = editorsField.GetValue(null) as IDictionary;
            var editorsList = editorDictionary[componentType] as IList;
            inspectorField.SetValue(editorsList[0], editorType);
#endif

            var inspectorType = Type.GetType("UnityEditor.InspectorWindow, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            var myTestMethod = inspectorType.GetMethod("RefreshInspectors", BindingFlags.NonPublic | BindingFlags.Static);
            myTestMethod.Invoke(null, null);
        }


        [InitializeOnLoadMethod]
        private static void DelayCallOverride()
        {
            EditorApplication.delayCall -= InitialOverride;
            EditorApplication.delayCall += InitialOverride;
        }

        private static void InitialOverride()
        {
            EditorApplication.delayCall -= InitialOverride;

            Type attributeType = Type.GetType("UnityEditor.CustomEditorAttributes, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

            try
            {
#if !UNITY_6000_0_OR_NEWER
                FieldInfo editorsInitializedField = attributeType.GetField("s_Initialized", BindingFlags.Static | BindingFlags.NonPublic);
                if (!(bool)editorsInitializedField.GetValue(null))
                {
                    MethodInfo rebuildEditorsMethod = attributeType.GetMethod("Rebuild", BindingFlags.Static | BindingFlags.NonPublic);
                    rebuildEditorsMethod.Invoke(null, null);
                    editorsInitializedField.SetValue(null, true);
                }
#endif

                OverrideEditor(typeof(VRCExpressionParameters), typeof(VRCParamsPlus));
                OverrideEditor(typeof(VRCExpressionsMenu), typeof(VRCMenuPlus));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[VRCSDK+] Failed to override editors!");
            }
        }

        #endregion
    }
}
