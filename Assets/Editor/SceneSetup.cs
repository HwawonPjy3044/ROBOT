using UnityEditor;
using UnityEngine;

public static class SceneSetup
{
    [MenuItem("Tools/Robot Arm/Build Demo Scene")]
    public static void BuildScene()
    {
        // Ground
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(2, 1, 2);

        // Conveyor belt (runs along +X, from spawn to pickup)
        GameObject conveyor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        conveyor.name = "ConveyorBelt";
        conveyor.transform.position = new Vector3(-3f, 0.25f, -2f);
        conveyor.transform.localScale = new Vector3(6f, 0.5f, 1.5f);
        SetColor(conveyor, new Color(0.2f, 0.2f, 0.2f), "Mat_Conveyor");

        GameObject spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = new Vector3(-6f, 0.75f, -2f);

        GameObject pickupPoint = new GameObject("PickupPoint");
        pickupPoint.transform.position = new Vector3(0f, 0.75f, -2f);

        // Box prefab
        GameObject boxProto = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxProto.name = "Box";
        boxProto.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        boxProto.AddComponent<BoxMover>();
        SetColor(boxProto, new Color(0.8f, 0.5f, 0.2f), "Mat_Box");

        const string prefabDir = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        string prefabPath = prefabDir + "/Box.prefab";
        GameObject boxPrefab = PrefabUtility.SaveAsPrefabAsset(boxProto, prefabPath);
        Object.DestroyImmediate(boxProto);

        // Spawner
        GameObject spawnerObj = new GameObject("BoxSpawner");
        BoxSpawner spawner = spawnerObj.AddComponent<BoxSpawner>();
        spawner.boxPrefab = boxPrefab;
        spawner.spawnPoint = spawnPoint.transform;
        spawner.pickupPoint = pickupPoint.transform;

        // Storage bin
        // Position it at ~70% of the arm's max reach (upperArm + foreArm), which keeps
        // both the near/far column and the bottom/top (3rd) layer comfortably reachable.
        const float upperArmLength = 1.2f;
        const float foreArmLength = 1.2f;
        const float boxWidth = 0.5f;
        float binDistance = (upperArmLength + foreArmLength) * 0.7f - boxWidth;

        GameObject bin = new GameObject("StorageBin");
        bin.transform.position = new Vector3(binDistance, 0f, 0f);
        BuildBinVisual(bin.transform, 1.4f, 0.8f);

        GameObject placementOrigin = new GameObject("PlacementOrigin");
        placementOrigin.transform.SetParent(bin.transform);
        placementOrigin.transform.localPosition = new Vector3(0f, 0.35f, 0f);

        StorageBin storageBin = bin.AddComponent<StorageBin>();
        storageBin.placementOrigin = placementOrigin.transform;
        storageBin.defaultBoxHeight = 0.5f;
        storageBin.maxBoxesPerColumn = 3;
        storageBin.maxColumns = 1;
        storageBin.columnSpacing = 0.6f;

        spawner.storageBin = storageBin;

        // Robot arm
        GameObject armRoot = new GameObject("RobotArm");
        armRoot.transform.position = Vector3.zero;

        GameObject baseVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseVisual.name = "BaseVisual";
        baseVisual.transform.SetParent(armRoot.transform);
        baseVisual.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        baseVisual.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
        SetColor(baseVisual, Color.gray, "Mat_ArmBase");

        GameObject baseJoint = new GameObject("BaseJoint");
        baseJoint.transform.SetParent(armRoot.transform);
        baseJoint.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        GameObject shoulderJoint = new GameObject("ShoulderJoint");
        shoulderJoint.transform.SetParent(baseJoint.transform);
        shoulderJoint.transform.localPosition = Vector3.zero;

        GameObject upperArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        upperArm.name = "UpperArmVisual";
        upperArm.transform.SetParent(shoulderJoint.transform);
        upperArm.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        upperArm.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
        SetColor(upperArm, new Color(0.9f, 0.6f, 0.1f), "Mat_ArmSegment");

        GameObject elbowJoint = new GameObject("ElbowJoint");
        elbowJoint.transform.SetParent(shoulderJoint.transform);
        elbowJoint.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        GameObject foreArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foreArm.name = "ForeArmVisual";
        foreArm.transform.SetParent(elbowJoint.transform);
        foreArm.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        foreArm.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
        SetColor(foreArm, new Color(0.9f, 0.6f, 0.1f), "Mat_ArmSegment");

        GameObject gripperAttach = new GameObject("GripperAttach");
        gripperAttach.transform.SetParent(elbowJoint.transform);
        gripperAttach.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        GameObject gripperVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gripperVisual.name = "GripperVisual";
        gripperVisual.transform.SetParent(gripperAttach.transform);
        gripperVisual.transform.localPosition = Vector3.zero;
        gripperVisual.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
        SetColor(gripperVisual, Color.red, "Mat_Gripper");

        RobotArmController arm = armRoot.AddComponent<RobotArmController>();
        arm.baseJoint = baseJoint.transform;
        arm.shoulderJoint = shoulderJoint.transform;
        arm.elbowJoint = elbowJoint.transform;
        arm.gripperAttach = gripperAttach.transform;
        arm.storageBin = storageBin;
        arm.pickupPoint = pickupPoint.transform;

        // Camera overview
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(5f, 6f, -6f);
            cam.transform.LookAt(new Vector3(1f, 0.5f, -1f));
        }

        Debug.Log("Robot arm demo scene built. Save the scene (Ctrl+S), then press Play.");
    }

    static void BuildBinVisual(Transform parent, float width, float depth)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "BinFloor";
        floor.transform.SetParent(parent);
        floor.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        floor.transform.localScale = new Vector3(width, 0.1f, depth);
        SetColor(floor, new Color(0.4f, 0.3f, 0.2f), "Mat_Bin");

        float halfW = width / 2f;
        float halfD = depth / 2f;

        Vector3[] wallOffsets =
        {
            new Vector3(halfW, 0.3f, 0f), new Vector3(-halfW, 0.3f, 0f),
            new Vector3(0f, 0.3f, halfD), new Vector3(0f, 0.3f, -halfD)
        };
        Vector3[] wallScales =
        {
            new Vector3(0.1f, 0.6f, depth), new Vector3(0.1f, 0.6f, depth),
            new Vector3(width, 0.6f, 0.1f), new Vector3(width, 0.6f, 0.1f)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "BinWall" + i;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = wallOffsets[i];
            wall.transform.localScale = wallScales[i];
            SetColor(wall, new Color(0.4f, 0.3f, 0.2f), "Mat_Bin");
        }
    }

    static void SetColor(GameObject go, Color color, string materialName)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = GetOrCreateMaterial(color, materialName);
    }

    static Material GetOrCreateMaterial(Color color, string materialName)
    {
        const string dir = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = dir + "/" + materialName + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material mat = new Material(shader);
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    [MenuItem("Tools/Robot Arm/Fix Box Material")]
    public static void FixBoxMaterial()
    {
        const string prefabPath = "Assets/Prefabs/Box.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogError("Box prefab not found at " + prefabPath);
            return;
        }

        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        Renderer r = instance.GetComponent<Renderer>();
        if (r != null)
            r.sharedMaterial = GetOrCreateMaterial(new Color(0.8f, 0.5f, 0.2f), "Mat_Box");

        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        PrefabUtility.UnloadPrefabContents(instance);
        AssetDatabase.SaveAssets();

        Debug.Log("Box material fixed.");
    }

    [MenuItem("Tools/Robot Arm/Upgrade To IK")]
    public static void UpgradeToIK()
    {
        GameObject armObj = GameObject.Find("RobotArm");
        GameObject pickup = GameObject.Find("PickupPoint");

        if (armObj == null || pickup == null)
        {
            Debug.LogError("RobotArm or PickupPoint not found in the open scene.");
            return;
        }

        RobotArmController arm = armObj.GetComponent<RobotArmController>();
        if (arm == null)
        {
            Debug.LogError("RobotArmController component not found on RobotArm.");
            return;
        }

        arm.pickupPoint = pickup.transform;
        EditorUtility.SetDirty(arm);

        Debug.Log("Arm upgraded to IK targeting. Pickup Point wired up. Save the scene and press Play.");
    }

    [MenuItem("Tools/Robot Arm/Resize Storage Bin (2x3)")]
    public static void ResizeStorageBin()
    {
        GameObject bin = GameObject.Find("StorageBin");
        if (bin == null)
        {
            Debug.LogError("StorageBin not found in the open scene.");
            return;
        }

        // Full clean rebuild: destroy every child (floor, walls, old placement origin)
        // rather than patching pieces, since repeated partial resizes had drifted.
        for (int i = bin.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(bin.transform.GetChild(i).gameObject);

        const float upperArmLength = 1.2f;
        const float foreArmLength = 1.2f;
        const float boxWidth = 0.5f;
        float binDistance = (upperArmLength + foreArmLength) * 0.7f - boxWidth;
        bin.transform.position = new Vector3(binDistance, 0f, 0f);

        BuildBinVisual(bin.transform, 1.4f, 0.8f);

        GameObject placementOrigin = new GameObject("PlacementOrigin");
        placementOrigin.transform.SetParent(bin.transform);
        placementOrigin.transform.localPosition = new Vector3(0f, 0.35f, 0f);

        StorageBin storageBin = bin.GetComponent<StorageBin>();
        if (storageBin == null) storageBin = bin.AddComponent<StorageBin>();
        storageBin.placementOrigin = placementOrigin.transform;
        storageBin.defaultBoxHeight = 0.5f;
        storageBin.maxBoxesPerColumn = 3;
        storageBin.maxColumns = 1;
        storageBin.columnSpacing = 0.6f;
        EditorUtility.SetDirty(storageBin);
        EditorUtility.SetDirty(bin);

        GameObject spawnerObj = GameObject.Find("BoxSpawner");
        if (spawnerObj != null)
        {
            BoxSpawner spawner = spawnerObj.GetComponent<BoxSpawner>();
            if (spawner != null)
            {
                spawner.storageBin = storageBin;
                EditorUtility.SetDirty(spawner);
            }
        }

        GameObject camObj = GameObject.Find("Main Camera");
        if (camObj != null)
        {
            camObj.transform.position = new Vector3(4f, 5f, -5f);
            camObj.transform.LookAt(new Vector3(binDistance, 0.5f, -1f));
            EditorUtility.SetDirty(camObj);
        }

        Debug.Log("Storage bin rebuilt cleanly at distance " + binDistance + " (2 columns x 3 rows, farthest column first).");
    }
}
