using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        #region Quick Avatar
        [MenuItem("CONTEXT/VRCAvatarDescriptor/[SDK+] Quick Setup", false, 650)]
        private static void QuickSetup(MenuCommand command)
        {
            VRCAvatarDescriptor desc = (VRCAvatarDescriptor)command.context;
            Animator ani = desc.GetComponent<Animator>();
            SerializedObject serialized = new SerializedObject(desc);

            if (ani)
            {
                // GetBoneTransform throws when the Animator has no Avatar, and only works on humanoid rigs.
                bool humanoid = ani.avatar && ani.isHuman;
                Transform leftEye = humanoid ? ani.GetBoneTransform(HumanBodyBones.LeftEye) : null;
                Transform rightEye = humanoid ? ani.GetBoneTransform(HumanBodyBones.RightEye) : null;

                Transform root = desc.transform;
                Transform head = humanoid ? ani.GetBoneTransform(HumanBodyBones.Head) : null;
                #region View Position
                // Worked out in the avatar's local space, so the result doesn't depend on where the avatar
                // stands or how it's rotated in the scene.
                Vector3? localView = null;
                if (leftEye && rightEye)
                {
                    Transform betterLeft = leftEye.parent.Find("LeftEye");
                    Transform betterRight = rightEye.parent.Find("RightEye");
                    leftEye = betterLeft ? betterLeft : leftEye;
                    rightEye = betterRight ? betterRight : rightEye;
                    localView = root.InverseTransformPoint((leftEye.position + rightEye.position) / 2);
                }
                else if (head)
                {
                    // No eye bones: estimate from the head, slightly above it, centred front-to-back on the avatar.
                    Vector3 localHead = root.InverseTransformPoint(head.position);
                    localView = new Vector3(localHead.x, localHead.y * 1.0357f, 0);
                }
                else
                {
                    // Generic rig or an Animator without a humanoid Avatar: there are no bones to measure from.
                    Debug.LogWarning("[VRCSDK+] Quick Setup couldn't find the head or eye bones (is the rig Humanoid?), so the View Position wasn't changed.");
                }

                if (localView.HasValue)
                {
                    Vector3 realView = localView.Value;
                    realView = new Vector3(Mathf.Approximately(realView.x, 0) ? 0 : realView.x, realView.y, (realView.z + 0.0547f * realView.y) / 2);
                    serialized.FindProperty("ViewPosition").vector3Value = realView;
                }
                #endregion

                #region Eyes

                if (leftEye && rightEye)
                {
                    SerializedProperty eyes = serialized.FindProperty("customEyeLookSettings");
                    serialized.FindProperty("enableEyeLook").boolValue = true;

                    eyes.FindPropertyRelative("leftEye").objectReferenceValue = leftEye;
                    eyes.FindPropertyRelative("rightEye").objectReferenceValue = rightEye;

                    #region Rotation Values
                    const float axisValue = 0.1305262f;
                    const float wValue = 0.9914449f;

                    Quaternion upValue = new Quaternion(-axisValue, 0, 0, wValue);
                    Quaternion downValue = new Quaternion(axisValue, 0, 0, wValue);
                    Quaternion rightValue = new Quaternion(0, axisValue, 0, wValue);
                    Quaternion leftValue = new Quaternion(0, -axisValue, 0, wValue);

                    SerializedProperty up = eyes.FindPropertyRelative("eyesLookingUp");
                    SerializedProperty right = eyes.FindPropertyRelative("eyesLookingRight");
                    SerializedProperty down = eyes.FindPropertyRelative("eyesLookingDown");
                    SerializedProperty left = eyes.FindPropertyRelative("eyesLookingLeft");

                    void SetLeftAndRight(SerializedProperty p, Quaternion v)
                    {
                        p.FindPropertyRelative("left").quaternionValue = v;
                        p.FindPropertyRelative("right").quaternionValue = v;
                    }

                    SetLeftAndRight(up, upValue);
                    SetLeftAndRight(right, rightValue);
                    SetLeftAndRight(down, downValue);
                    SetLeftAndRight(left, leftValue);
                    #endregion

                    #region Blinking
                    SkinnedMeshRenderer body = null;
                    for (int i = 0; i < desc.transform.childCount; i++)
                    {
                        if (body = desc.transform.GetChild(i).GetComponent<SkinnedMeshRenderer>())
                            break;
                    }

                    if (body && body.sharedMesh)
                    {
                        for (int i = 0; i < body.sharedMesh.blendShapeCount; i++)
                        {
                            if (body.sharedMesh.GetBlendShapeName(i) != "Blink") continue;

                            eyes.FindPropertyRelative("eyelidType").enumValueIndex = 2;
                            eyes.FindPropertyRelative("eyelidsSkinnedMesh").objectReferenceValue = body;

                            SerializedProperty blendShapes = eyes.FindPropertyRelative("eyelidsBlendshapes");
                            blendShapes.arraySize = 3;
                            blendShapes.FindPropertyRelative("Array.data[0]").intValue = i;
                            blendShapes.FindPropertyRelative("Array.data[1]").intValue = -1;
                            blendShapes.FindPropertyRelative("Array.data[2]").intValue = -1;
                            break;
                        }
                    }
                    #endregion
                }
                #endregion
            }

            serialized.ApplyModifiedProperties();
            quickSetupDescriptor = desc;
            EditorApplication.delayCall -= ForceCallAutoLipSync;
            EditorApplication.delayCall += ForceCallAutoLipSync;
        }

        // The descriptor Quick Setup ran on, so lip sync is detected for that avatar and not another inspected one.
        private static VRCAvatarDescriptor quickSetupDescriptor;

        private static void ForceCallAutoLipSync()
        {
            EditorApplication.delayCall -= ForceCallAutoLipSync;
            VRCAvatarDescriptor desc = quickSetupDescriptor;
            quickSetupDescriptor = null;
            if (!desc) return;

            var descriptorEditor = Type.GetType("AvatarDescriptorEditor3, Assembly-CSharp-Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") ?? 
                                   Type.GetType("AvatarDescriptorEditor3, VRC.SDK3A.Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

            if (descriptorEditor == null)
            {
                Debug.LogWarning("AvatarDescriptorEditor3 Type couldn't be found!");
                return;
            }
            
            // Use the descriptor editor that is showing this avatar (there may be none, or several for different avatars).
            Editor tempEditor = Resources.FindObjectsOfTypeAll(descriptorEditor).OfType<Editor>().FirstOrDefault(e => e && e.target == desc);
            MethodInfo autoDetect = descriptorEditor.GetMethod("AutoDetectLipSync", BindingFlags.NonPublic | BindingFlags.Instance);
            if (tempEditor == null || autoDetect == null)
            {
                Debug.LogWarning("[VRCSDK+] Couldn't run lip sync auto-detection: no Avatar Descriptor inspector is showing this avatar.");
                return;
            }
            autoDetect.Invoke(tempEditor, null);
        }

        #endregion
    }
}
