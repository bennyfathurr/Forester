using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    public sealed class GridCellView:MonoBehaviour {
        public Cell cell;
        public string Id=>cell.Id;
    }
}
