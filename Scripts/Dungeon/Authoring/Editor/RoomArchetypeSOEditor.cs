using System.Collections.Generic;
using Edgar.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Room Prefab의 Doors(Grid2D) 데이터를 읽어 RoomArchetypeSO._connections를 보조 작성한다.
/// Edgar가 런타임에 생성하는 Door 위치와 동일한 소스(Tilemap/Doors 컴포넌트)에서 값을 가져오므로
/// EdgarGenerationAdapter.TryResolveConnectionId가 기대하는 Cell 기준과 어긋나지 않는다.
/// </summary>
[CustomEditor(typeof(RoomArchetypeSO))]
public sealed class RoomArchetypeSOEditor : Editor
{
    private struct ExtractedDoor
    {
        public Vector2Int Cell;
        public RoomConnectionDirection Direction;
        public int PassageWidthInCells;
    }

    private struct DoorLine
    {
        public Vector3Int From;
        public Vector3Int To;
        public int Length;
    }

    private struct TileBounds
    {
        public int MinX;
        public int MaxX;
        public int MinY;
        public int MaxY;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Connection 자동 추출", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Room Prefab의 Doors(Grid2D) 컴포넌트에서 Cell/Direction/Width를 읽어 비어 있는 Connection만 "
            + "새로 추가합니다. 기존 ConnectionId·ConnectionTypeId는 그대로 유지됩니다.",
            MessageType.Info);

        if (GUILayout.Button("프리팹에서 Connection 추출"))
        {
            ExtractConnectionsFromPrefab();
        }

        DrawDoorAndSpawnSummary();
    }

    // 현재 Room의 출입구·Spawn 지점 구성을 요약하고 DungeonDataValidator 오류를 표시한다. Asset은 수정하지 않는다.
    private void DrawDoorAndSpawnSummary()
    {
        var room = (RoomArchetypeSO)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Door·Spawn 요약", EditorStyles.boldLabel);

        int connectionCount = room.Connections?.Count ?? 0;
        EditorGUILayout.LabelField($"Door(Connection): {connectionCount}개");

        var spawnPointCountsByKind = new Dictionary<DungeonSpawnPointKind, int>();

        if (room.SpawnPoints != null)
        {
            foreach (RoomSpawnPointDefinition spawnPoint in room.SpawnPoints)
            {
                if (spawnPoint == null)
                {
                    continue;
                }

                spawnPointCountsByKind.TryGetValue(spawnPoint.Kind, out int count);
                spawnPointCountsByKind[spawnPoint.Kind] = count + 1;
            }
        }

        foreach (DungeonSpawnPointKind kind in System.Enum.GetValues(typeof(DungeonSpawnPointKind)))
        {
            spawnPointCountsByKind.TryGetValue(kind, out int count);
            EditorGUILayout.LabelField($"Spawn Point [{kind}]: {count}개");
        }

        DungeonDataValidationResult result = DungeonDataValidator.ValidateRoomDoorsAndSpawnPoints(room);

        if (result.HasErrors)
        {
            for (int i = 0; i < result.Errors.Count; i++)
            {
                EditorGUILayout.HelpBox(result.Errors[i].Message, MessageType.Error);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Door·Spawn 구성에 오류가 없습니다.", MessageType.Info);
        }
    }

    private void ExtractConnectionsFromPrefab()
    {
        SerializedProperty roomPrefabProperty = serializedObject.FindProperty("_roomPrefab");
        var roomPrefab = roomPrefabProperty.objectReferenceValue as GameObject;

        if (roomPrefab == null)
        {
            EditorUtility.DisplayDialog("Connection 추출 실패", "Room Prefab이 지정되지 않았습니다.", "확인");
            return;
        }

        var doors = roomPrefab.GetComponent<DoorsGrid2D>();
        if (doors == null)
        {
            EditorUtility.DisplayDialog(
                "Connection 추출 실패",
                "Room Prefab에 Doors(Grid2D) 컴포넌트가 없습니다.",
                "확인");
            return;
        }

        List<ExtractedDoor> extracted = ExtractDoors(roomPrefab, doors);
        if (extracted.Count == 0)
        {
            EditorUtility.DisplayDialog("Connection 추출", "Room Prefab에서 출입구를 찾지 못했습니다.", "확인");
            return;
        }

        SerializedProperty connectionsProperty = serializedObject.FindProperty("_connections");
        var existingCells = new HashSet<Vector2Int>();
        for (int i = 0; i < connectionsProperty.arraySize; i++)
        {
            SerializedProperty element = connectionsProperty.GetArrayElementAtIndex(i);
            existingCells.Add(element.FindPropertyRelative("_cell").vector2IntValue);
        }

        int addedCount = 0;
        int nextIndex = connectionsProperty.arraySize;

        foreach (ExtractedDoor door in extracted)
        {
            if (existingCells.Contains(door.Cell))
            {
                continue;
            }

            connectionsProperty.InsertArrayElementAtIndex(connectionsProperty.arraySize);
            SerializedProperty element = connectionsProperty.GetArrayElementAtIndex(connectionsProperty.arraySize - 1);
            element.FindPropertyRelative("_connectionId").stringValue = $"Door_{nextIndex}";
            element.FindPropertyRelative("_cell").vector2IntValue = door.Cell;
            element.FindPropertyRelative("_direction").enumValueIndex = (int)door.Direction;
            element.FindPropertyRelative("_connectionTypeId").stringValue =
                RoomConnectionDefinition.StandardConnectionTypeId;
            element.FindPropertyRelative("_passageWidthInCells").intValue = Mathf.Max(1, door.PassageWidthInCells);

            nextIndex++;
            addedCount++;
        }

        serializedObject.ApplyModifiedProperties();

        EditorUtility.DisplayDialog(
            "Connection 추출 완료",
            addedCount > 0
                ? $"새 Connection {addedCount}개를 추가했습니다. ConnectionId와 ConnectionTypeId를 확인해 주세요."
                : "추출된 모든 Door가 이미 등록되어 있어 추가된 항목이 없습니다.",
            "확인");
    }

    private static List<ExtractedDoor> ExtractDoors(GameObject roomPrefab, DoorsGrid2D doors)
    {
        List<DoorLine> lines = CollectDoorLines(doors);
        if (lines.Count == 0)
        {
            return new List<ExtractedDoor>();
        }

        List<Tilemap> outlineTilemaps = RoomTemplateUtilsGrid2D.GetTilemapsForOutline(
            RoomTemplateUtilsGrid2D.GetTilemaps(roomPrefab));
        HashSet<Vector2Int> usedTiles = RoomTemplateUtilsGrid2D.GetUsedTiles(outlineTilemaps);
        TileBounds bounds = ComputeBounds(usedTiles);

        var result = new List<ExtractedDoor>(lines.Count);
        foreach (DoorLine line in lines)
        {
            // Manual Door 모드는 From/To 순서가 Edgar에 의해 정규화되지 않으므로(위→아래로 드래그 시
            // From.y > To.y 가능) RoomConnectionDefinition._cell 계약(최소 좌표)에 맞춰 직접 정규화한다.
            Vector2Int minCell = new Vector2Int(
                Mathf.Min(line.From.x, line.To.x),
                Mathf.Min(line.From.y, line.To.y));

            result.Add(new ExtractedDoor
            {
                Cell = minCell,
                Direction = ResolveDirection(minCell, bounds),
                PassageWidthInCells = line.Length,
            });
        }

        return result;
    }

    // Manual/Simple/Hybrid Door 모드를 동일한 DoorLine 표현으로 맞춘다.
    private static List<DoorLine> CollectDoorLines(DoorsGrid2D doors)
    {
        var lines = new List<DoorLine>();

        switch (doors.SelectedMode)
        {
            case DoorsGrid2D.DoorMode.Manual:
                foreach (DoorGrid2D door in doors.ManualDoorModeData.DoorsList)
                {
                    Vector3Int from = Vector3Int.RoundToInt(door.From);
                    Vector3Int to = Vector3Int.RoundToInt(door.To);
                    int length = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y)) + 1;
                    lines.Add(new DoorLine { From = from, To = to, Length = length });
                }
                break;

            case DoorsGrid2D.DoorMode.Simple:
                List<DoorLineGrid2D> simpleLines = doors.SimpleDoorModeData.GetDoorLines(doors);
                if (simpleLines != null)
                {
                    foreach (DoorLineGrid2D line in simpleLines)
                    {
                        lines.Add(new DoorLine { From = line.From, To = line.To, Length = line.Length });
                    }
                }
                break;

            case DoorsGrid2D.DoorMode.Hybrid:
                foreach (DoorLineGrid2D line in doors.HybridDoorModeData.DoorLines)
                {
                    lines.Add(new DoorLine { From = line.From, To = line.To, Length = line.Length });
                }
                break;
        }

        return lines;
    }

    private static TileBounds ComputeBounds(HashSet<Vector2Int> tiles)
    {
        var bounds = new TileBounds { MinX = int.MaxValue, MaxX = int.MinValue, MinY = int.MaxValue, MaxY = int.MinValue };

        foreach (Vector2Int tile in tiles)
        {
            bounds.MinX = Mathf.Min(bounds.MinX, tile.x);
            bounds.MaxX = Mathf.Max(bounds.MaxX, tile.x);
            bounds.MinY = Mathf.Min(bounds.MinY, tile.y);
            bounds.MaxY = Mathf.Max(bounds.MaxY, tile.y);
        }

        return bounds;
    }

    // Door Line이 Room 타일 범위 중 어느 쪽 경계에 가장 가까운지로 Direction을 추정한다.
    private static RoomConnectionDirection ResolveDirection(Vector2Int from, TileBounds bounds)
    {
        int distanceToTop = Mathf.Abs(bounds.MaxY - from.y);
        int distanceToBottom = Mathf.Abs(from.y - bounds.MinY);
        int distanceToRight = Mathf.Abs(bounds.MaxX - from.x);
        int distanceToLeft = Mathf.Abs(from.x - bounds.MinX);

        int verticalDistance = Mathf.Min(distanceToTop, distanceToBottom);
        int horizontalDistance = Mathf.Min(distanceToRight, distanceToLeft);

        if (verticalDistance <= horizontalDistance)
        {
            return distanceToTop <= distanceToBottom ? RoomConnectionDirection.Up : RoomConnectionDirection.Down;
        }

        return distanceToRight <= distanceToLeft ? RoomConnectionDirection.Right : RoomConnectionDirection.Left;
    }
}
