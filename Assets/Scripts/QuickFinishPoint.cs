using UnityEngine;

// Transparent hotspots are routed by QuickFinishGame, never by OnMouseDown.
[RequireComponent(typeof(BoxCollider2D))]
public class QuickFinishPoint : MonoBehaviour
{
    public enum Kind { Poster, Graffiti, Coin, NPC }
    public Kind kind;
    public int coinId;
    public QuickFinishGame game;

    public void Interact()
    {
        if (game != null) game.Interact(this);
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = kind == Kind.Coin ? Color.yellow : Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.offset, box.size);
    }
}
