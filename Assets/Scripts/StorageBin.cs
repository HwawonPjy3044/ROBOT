using UnityEngine;

public class StorageBin : MonoBehaviour
{
    public Transform placementOrigin;
    public float defaultBoxHeight = 0.5f; // used only if a box has no Renderer
    public int maxBoxesPerColumn = 3;     // layers (height)
    public int maxColumns = 1;            // side-by-side columns (width)
    public float columnSpacing = 0.6f;

    int boxesInColumn = 0;
    float stackedHeight = 0f; // sum of the actual heights of boxes placed so far in this column
    int columnIndex;
    int totalPlaced = 0;

    public bool IsFull => totalPlaced >= maxColumns * maxBoxesPerColumn;

    void Awake()
    {
        columnIndex = maxColumns - 1; // fill the column farthest from the arm first
    }

    public Vector3 GetNextPlacementPosition()
    {
        float centeredX = (columnIndex - (maxColumns - 1) / 2f) * columnSpacing;
        Vector3 columnOffset = new Vector3(centeredX, 0f, 0f);
        return placementOrigin.position + columnOffset + Vector3.up * stackedHeight;
    }

    public void ConfirmPlaced(GameObject box)
    {
        float height = defaultBoxHeight;
        if (box != null)
        {
            Renderer r = box.GetComponentInChildren<Renderer>();
            if (r != null) height = r.bounds.size.y;
        }

        stackedHeight += height;
        boxesInColumn++;
        totalPlaced++;

        if (boxesInColumn >= maxBoxesPerColumn)
        {
            boxesInColumn = 0;
            stackedHeight = 0f;
            columnIndex--;
            if (columnIndex < 0)
                columnIndex = maxColumns - 1;
        }
    }
}
