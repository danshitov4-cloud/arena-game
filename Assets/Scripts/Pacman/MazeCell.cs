using UnityEngine;

namespace PacmanGame
{
    public enum MazeCellType { Path, Wall }

    public class MazeCell : MonoBehaviour
    {
        [SerializeField] private MazeCellType cellType;
        public MazeCellType CellType => cellType;
        public bool IsWall => cellType == MazeCellType.Wall;

        public void Configure(MazeCellType type)
        {
            cellType = type;
        }
    }
}