using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;


public enum SpecialMove
{
    None = 0,
    EnPassant,
    Casteling,
    Promotion
}
public class ChessBoard : MonoBehaviour
{
    [Header("Art things")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tilesize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f, deadSpacing = 0.3f, dragOffset = 1.5f;
    [SerializeField] private GameObject victoryScreen;



    [Header("Prefabs and materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    //Logic Field
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDraging;
    private const int TILECOUNTX = 8;
    private const int TILECOUNTY = 8;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private SpecialMove specialMove;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int CurrentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;



    private void Awake()
    {
        isWhiteTurn = true;
        generateAllTiles(tilesize, TILECOUNTX, TILECOUNTY);
        SpawnAllPieces();
        positionAllPieces();
    }
    private void Update()
    {
        if(!currentCamera )
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile","Hover","Highlight")))
        {
            //Get the indexes of touched files
            Vector2Int hitPosition = LookUpTileIndex(info.transform.gameObject);

            //If we were not on tile before
            if(CurrentHover == -Vector2Int.one)
            {
                CurrentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }


            //If we were on tile before
            if (CurrentHover != hitPosition)
            {

                tiles[CurrentHover.x, CurrentHover.y].layer = (ContainsValidMove(ref availableMoves, CurrentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
                CurrentHover = hitPosition;
            }

            //If we press down on the mouse
            if(Input.GetMouseButtonDown(0))
            {
                if (chessPieces[hitPosition.x, hitPosition.y] != null) 
                {
                    //Is your turn
                    if((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn))
                    {
                        currentlyDraging = chessPieces[hitPosition.x,hitPosition.y];
                        //Get Available moves and highlite tiles as well
                        availableMoves = currentlyDraging.GetAvailableMoves(ref chessPieces, TILECOUNTX, TILECOUNTY);
                        //Get a list of special moves as well
                        specialMove = currentlyDraging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);


                        PreventCheck();
                        HighlightTiles();
                        

                    }
                }
            }

            //If we release mouse down button
            if(currentlyDraging != null && Input.GetMouseButtonUp(0)) 
            { 
                Vector2Int previousPosition = new Vector2Int(currentlyDraging.currentX,currentlyDraging.currentY);

                
                bool validMove = MoveTo(currentlyDraging, hitPosition.x, hitPosition.y);
                if (!validMove)
                    currentlyDraging.SetPosition(getTileCentre(previousPosition.x, previousPosition.y));   
                        
                currentlyDraging = null;
                RemoveHighlightTiles();
            }

        }
        else
        {

            if (CurrentHover != -Vector2Int.one)
            {
                tiles[CurrentHover.x, CurrentHover.y].layer = (ContainsValidMove(ref availableMoves, CurrentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                CurrentHover = -Vector2Int.one;
            }

            if (currentlyDraging && Input.GetMouseButtonUp(0))
            {
                currentlyDraging.SetPosition(getTileCentre(currentlyDraging.currentX, currentlyDraging.currentY));
                currentlyDraging = null;
                RemoveHighlightTiles();
            }

        }

        //If we are dragging a piece
        if(currentlyDraging) 
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))
                currentlyDraging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }   



    }

    //Generate the board
    private void generateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3 ((tileCountX/2) * tileSize,0, (tileCountX / 2) * tileSize) + boardCenter;



        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    { 
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}",x,y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;
        vertices[2] = new Vector3((x+1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x+1) * tileSize, yOffset, (y+1) * tileSize) - bounds;

        int[] tris = new int [] { 0, 1, 2, 1, 3, 2 }; 

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    //Spawning pieces

    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILECOUNTX, TILECOUNTY];

        int whiteTeam = 0, blackTeam = 1;

        //White team
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for (int i = 0; i < TILECOUNTX; i++)
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);

        //Black team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        for (int i = 0; i < TILECOUNTX; i++)
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);

    }

    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    //Positioning

    private void positionAllPieces()
    {
        for (int X = 0; X < TILECOUNTX; X++)
            for (int y = 0; y < TILECOUNTY; y++)
                if (chessPieces[X, y] != null)
                    positionSinglePiece(X, y, true);

        
    }
    private void positionSinglePiece(int x, int y, bool forse = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(getTileCentre(x, y), forse);
    }
    private Vector3 getTileCentre(int x, int y) 
    {
        return new Vector3(x * tilesize, yOffset, y * tilesize) - bounds + new Vector3(tilesize/2, 0, tilesize/2);
    }

    //Highliting tiles
    private void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        
    }
    private void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        
        availableMoves.Clear();
    }
    //CheckMate

    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }
    public void OnResetButton()
    {
        //UI
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        //Field reset
        currentlyDraging = null;
        availableMoves.Clear();
        moveList.Clear();
        //Clean up
        for (int x = 0; x < TILECOUNTX; x++)
        {
            for (int y = 0; y < TILECOUNTY; y++)
            {
                if (chessPieces[x, y] != null)
                    Destroy(chessPieces[x, y].gameObject);

                chessPieces[x, y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);

        deadBlacks.Clear();
        deadWhites.Clear();
        

        SpawnAllPieces();
        positionAllPieces();
        isWhiteTurn = true;
    }
    public void OnExitButton()
    {
        Application.Quit();
    }

    //Special Moves
    private void ProcessSpecialMove()
    {
        if (specialMove == SpecialMove.EnPassant) 
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];
            if (myPawn.currentY == enemyPawn.currentY) 
            {
                
                
                if (myPawn.currentX == enemyPawn.currentX - 1 || myPawn.currentX == enemyPawn.currentX + 1) 
                {

                    if (enemyPawn.team == 0)
                    { 
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tilesize, yOffset, -1 * tilesize) - bounds + new Vector3(tilesize / 2, 0, tilesize / 2) + (Vector3.forward * deadSpacing) * deadWhites.Count);

                    }
                    else
                    { 
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(-1 * tilesize, yOffset, 8 * tilesize) - bounds + new Vector3(tilesize / 2, 0, tilesize / 2) + (Vector3.back * deadSpacing) * deadBlacks.Count);
                    }
                }
                chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
            }
        }
        
        if (specialMove == SpecialMove.Promotion) 
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if (targetPawn.type == ChessPieceType.Pawn)
            {
                if (targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen =  SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    positionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    positionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
            }
        }

        if (specialMove == SpecialMove.Casteling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            //Left Rook
            if (lastMove[1].x == 2 )
            {
                if(lastMove[1].y == 0)
                {
                    ChessPiece rook  = chessPieces[0, 0]; //White side
                    chessPieces[3, 0] = rook;
                    positionSinglePiece(3, 0);
                    chessPieces[0, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[0, 7]; //Black side
                    chessPieces[3, 7] = rook;
                    positionSinglePiece(3, 7);
                    chessPieces[0, 7] = null;
                }
            }
            //Right rook
            else if (lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[7, 0]; //White side
                    chessPieces[5, 0] = rook;
                    positionSinglePiece(5, 0);
                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[7, 7]; //Black side
                    chessPieces[5, 7] = rook;
                    positionSinglePiece(5, 7);
                    chessPieces[7, 7] = null;
                }
            }

        }

    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;

        for (int x = 0; x < TILECOUNTX; x++)
            for (int y = 0; y < TILECOUNTY; y++)
                if (chessPieces[x, y] != null)
                    if (chessPieces[x, y].type == ChessPieceType.King)
                        if (chessPieces[x, y].team == currentlyDraging.team)
                            targetKing = chessPieces[x, y];

        SimulateMoveForSinglePiece(currentlyDraging,ref availableMoves,targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        // Save current values to reset after function call
        int actualX = cp.currentX;
        int actualY = cp.currentY;

        // Checking all the moves looking for check
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSimulation = (cp.type == ChessPieceType.King) ? new Vector2Int(simX, simY) : new Vector2Int(targetKing.currentX, targetKing.currentY);

            // Copy the [,] array and not the reference
            ChessPiece[,] simulation = new ChessPiece[TILECOUNTX, TILECOUNTY];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();

            for (int x = 0; x < TILECOUNTX; x++)
            {
                for (int y = 0; y < TILECOUNTY; y++)
                {
                    if (chessPieces[x, y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                            simAttackingPieces.Add(simulation[x, y]);
                    }
                }
            }

            // Simulate that move
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            // Did one of the pieces get taken?
            var deadPiece = simAttackingPieces.Find(c => c.currentX == simX && c.currentY == simY);
            if (deadPiece != null)
                simAttackingPieces.Remove(deadPiece);

            // Get all the simulated attacking pieces' moves
            List<Vector2Int> simMoves = new List<Vector2Int>();

            for (int a = 0; a < simAttackingPieces.Count; a++)
            {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, TILECOUNTX, TILECOUNTY);
                simMoves.AddRange(pieceMoves);
            }

            // Is the king being attacked? If so, remove the move
            if (ContainsValidMove(ref simMoves, kingPositionThisSimulation))
            {
                moves.RemoveAt(i);
                i--; // Adjust the index to account for the removed element
            }

            // Restore actual cp data
            cp.currentX = actualX;
            cp.currentY = actualY;
        }
    }

    private bool CheckForCheckmate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;

        for (int x = 0; x < TILECOUNTX; x++)
        {
            for (int y = 0; y < TILECOUNTY; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                            targetKing = chessPieces[x, y];
                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                }
            }
        }

        // Check if the king is currently under attack
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();

        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILECOUNTX, TILECOUNTY);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                currentAvailableMoves.Add(pieceMoves[b]);
            }
        }

        // Check if the king is in check
        if (ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            
            // Check if any defending piece can help the king
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILECOUNTX, TILECOUNTY);
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0)
                {
                    return false;
                }
            }

            return true; // Checkmate condition
        }

        return false;
    }


    //Operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        for (int i = 0; i < moves.Count; i++)
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;

        return false;
    }
    private bool MoveTo(ChessPiece cp, int x, int y)
    {
        if (!ContainsValidMove(ref availableMoves, new Vector2Int(x, y))) 
            return false;


        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);

        //Is there another piece on the target pos
        if (chessPieces[x,y] != null)
        {

            ChessPiece ocp = chessPieces[x,y];
            if (cp.team == ocp.team)
            return false;

            //If it's enemy piece
            if (ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);


                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tilesize, yOffset, -1 * tilesize) - bounds + new Vector3(tilesize/2,0,tilesize/2) + (Vector3.forward * deadSpacing) * deadWhites.Count);

            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);

                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tilesize, yOffset, 8 * tilesize) - bounds + new Vector3(tilesize / 2, 0, tilesize / 2) + (Vector3.back * deadSpacing) * deadBlacks.Count);

            }

        }

        chessPieces[x,y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        isWhiteTurn = !isWhiteTurn;
        moveList.Add(new Vector2Int[] {previousPosition, new Vector2Int(x, y) });
        ProcessSpecialMove();

        if (CheckForCheckmate())
            CheckMate(cp.team);

        positionSinglePiece(x, y);
        return true;
    }
    private Vector2Int LookUpTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILECOUNTX; x++)
            for (int y = 0; y < TILECOUNTY; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);

        return -Vector2Int.one;  //Invalid
   }
}
