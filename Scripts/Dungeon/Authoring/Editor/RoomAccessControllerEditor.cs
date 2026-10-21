using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Room Prefab 자식의 "Door_*" GameObject에서 RoomAccessController._doors의 ConnectionId·Anchor를
/// 자동으로 찾아 연결한다. ConnectionId는 Door GameObject 이름의 "Door_" 접두사를 제거하고 소문자로
/// 변환해 얻는다(예: "Door_Right" → "right") — 프로젝트의 모든 Room Archetype이 이 소문자 방향 표기를
/// 쓰고 있음을 확인했다.
/// </summary>
[CustomEditor(typeof(RoomAccessController))]
public sealed class RoomAccessControllerEditor : Editor
{
    private const string DoorNamePrefix = "Door_";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Door 자동 연결", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "자식 중 이름이 \"Door_\"로 시작하는 GameObject를 찾아 ConnectionId(이름에서 소문자로 추출)와 "
            + "Anchor를 채웁니다. 목록이 비어 있으면 새로 채우고, 이미 항목이 있으면 비어 있는 Anchor만 "
            + "같은 ConnectionId의 Door로 채웁니다. 기존 값은 덮어쓰지 않습니다.",
            MessageType.Info);

        if (GUILayout.Button("자식 Door에서 자동 연결"))
        {
            AutoWireDoors();
        }
    }

    private void AutoWireDoors()
    {
        var controller = (RoomAccessController)target;

        Dictionary<string, List<Transform>> doorsByConnectionId = CollectDoorTransformsByConnectionId(controller);

        serializedObject.Update();
        SerializedProperty doors = serializedObject.FindProperty("_doors");
        var warnings = new List<string>();
        int changedCount = doors.arraySize == 0
            ? FillFromScratch(doors, doorsByConnectionId, warnings)
            : FillEmptyAnchors(doors, doorsByConnectionId, warnings);

        serializedObject.ApplyModifiedProperties();

        string message = changedCount > 0
            ? $"Door 항목 {changedCount}개를 채웠습니다."
            : "채울 항목이 없습니다(이미 모두 연결되어 있거나 대응하는 Door_* 자식이 없음).";

        if (warnings.Count > 0)
        {
            message += "\n\n" + string.Join("\n", warnings);
        }

        EditorUtility.DisplayDialog("Door 자동 연결", message, "확인");
    }

    private static Dictionary<string, List<Transform>> CollectDoorTransformsByConnectionId(
        RoomAccessController controller)
    {
        var result = new Dictionary<string, List<Transform>>();
        Transform[] candidates = controller.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];

            if (candidate == controller.transform || !candidate.name.StartsWith(DoorNamePrefix))
            {
                continue;
            }

            string connectionId = candidate.name.Substring(DoorNamePrefix.Length).ToLowerInvariant();

            if (!result.TryGetValue(connectionId, out List<Transform> list))
            {
                list = new List<Transform>();
                result[connectionId] = list;
            }

            list.Add(candidate);
        }

        return result;
    }

    // _doors가 비어 있을 때 발견된 Door_* 전부를 새 항목으로 추가한다.
    private static int FillFromScratch(
        SerializedProperty doors, Dictionary<string, List<Transform>> doorsByConnectionId, List<string> warnings)
    {
        int addedCount = 0;

        foreach (KeyValuePair<string, List<Transform>> entry in doorsByConnectionId)
        {
            if (entry.Value.Count > 1)
            {
                warnings.Add($"ConnectionId '{entry.Key}' 후보가 {entry.Value.Count}개 발견되어 건너뛰었습니다.");
                continue;
            }

            int index = doors.arraySize;
            doors.InsertArrayElementAtIndex(index);
            SerializedProperty element = doors.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("_connectionId").stringValue = entry.Key;
            element.FindPropertyRelative("_anchor").objectReferenceValue = entry.Value[0];
            addedCount++;
        }

        return addedCount;
    }

    // _doors에 이미 항목이 있으면 비어 있는 Anchor만 같은 ConnectionId의 Door로 채운다.
    private static int FillEmptyAnchors(
        SerializedProperty doors, Dictionary<string, List<Transform>> doorsByConnectionId, List<string> warnings)
    {
        int filledCount = 0;

        for (int i = 0; i < doors.arraySize; i++)
        {
            SerializedProperty element = doors.GetArrayElementAtIndex(i);
            SerializedProperty anchorProperty = element.FindPropertyRelative("_anchor");

            if (anchorProperty.objectReferenceValue != null)
            {
                continue;
            }

            string connectionId = element.FindPropertyRelative("_connectionId").stringValue;

            if (string.IsNullOrWhiteSpace(connectionId))
            {
                continue;
            }

            string key = connectionId.ToLowerInvariant();

            if (!doorsByConnectionId.TryGetValue(key, out List<Transform> candidates))
            {
                warnings.Add($"ConnectionId '{connectionId}'에 대응하는 Door_* 자식을 찾지 못했습니다.");
                continue;
            }

            if (candidates.Count > 1)
            {
                warnings.Add($"ConnectionId '{connectionId}' 후보가 {candidates.Count}개 발견되어 건너뛰었습니다.");
                continue;
            }

            anchorProperty.objectReferenceValue = candidates[0];
            filledCount++;
        }

        return filledCount;
    }
}
