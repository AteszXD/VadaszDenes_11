class Node
{
    public int X, Y;
    public int G; // cost from start
    public int H; // heuristic to goal
    public int F => G + H;
    public Node Parent;

    public Node(int x, int y, Node parent = null)
    {
        X = x; Y = y; Parent = parent;
    }
}