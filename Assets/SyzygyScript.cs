using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System;

public class SyzygyScript : MonoBehaviour {

    //Public global variables
    public KMAudio audio;
    public KMBombInfo bomb;
    public KMSelectable[] buttons;
    public SpriteRenderer[] symbols;
    public Sprite[] sprites;
    public MeshRenderer[] tiles;
    public Material[] mats;
    public MeshRenderer backing;
    public Material backMatTemplate;

    //Private global variables
    private Material backMat;
    private int[] symbolIndices = { 0, 1, 2, 5, 4, 3, 6, 7, 8, 9, 10 };
    private int[] tileColors;
    private int[] solverSolution;
    private int selected = -1;

    //Global variables for logging and module solved status
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        //Logging ID and module button press setup
        moduleId = moduleIdCounter++;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressTile(pressed); return false; };
        }
        //Copy background material for each instance of the mod since it's modified
        backMat = new Material(backMatTemplate);
        backing.material = backMat;
        StartCoroutine(MovingBackground());
    }

    void Start()
    {
        redo:
        //Randomize the locked tiles
        tileColors = new int[11];
        for (int i = 0; i < UnityEngine.Random.Range(1, 4); i++)
            tileColors[i] = 1;
        tileColors = tileColors.Shuffle();
        List<int> avoidSwapping = new List<int>();
        //Prepare even last digit puzzle
        if (bomb.GetSerialNumberNumbers().Last() % 2 == 0)
        {
            //Make sure Terra stays in the center
            avoidSwapping.Add(5);
            //Force Pluto to left side
            int choice = UnityEngine.Random.Range(0, 5);
            Swap(Array.IndexOf(symbolIndices, 10), choice);
            avoidSwapping.Add(choice);
            //Force Luna to be adjacent to Terra
            int choice2 = UnityEngine.Random.Range(4, 7);
            while (choice2 == 5 || (choice == 4 && choice2 == 4))
                choice2 = UnityEngine.Random.Range(4, 7);
            Swap(Array.IndexOf(symbolIndices, 4), choice2);
            avoidSwapping.Add(choice2);
        }
        //Prepare odd last digit puzzle
        else
        {
            //Make sure Sol stays leftmost
            avoidSwapping.Add(0);
            //Force Neptunus to right side
            int choice = UnityEngine.Random.Range(6, 11);
            Swap(Array.IndexOf(symbolIndices, 9), choice);
            avoidSwapping.Add(choice);
        }
        //Swap around tiles until puzzle is valid, excluding locked tiles and those in avoidSwapping
        int swapCt = 0;
        while (true)
        {
            int choice1 = UnityEngine.Random.Range(0, 11);
            int choice2 = UnityEngine.Random.Range(0, 11);
            while (tileColors[choice1] == 1 || avoidSwapping.Contains(choice1))
                choice1 = UnityEngine.Random.Range(0, 11);
            while (tileColors[choice2] == 1 || avoidSwapping.Contains(choice2) || choice1 == choice2)
                choice2 = UnityEngine.Random.Range(0, 11);
            Swap(choice1, choice2);
            if (!CheckValidity())
            {
                swapCt++;
                if (swapCt == 100) //If we reach 100 swaps and the puzzle is still not valid, lets regenerate
                    goto redo;
            }
            else
                break;
        }
        //Store the intended solution in case the Twitch Plays solver is called
        solverSolution = symbolIndices.ToArray();
        //Log the intended solution
        string log = "";
        for (int i = 0; i < 11; i++)
        {
            log += sprites[symbolIndices[i]].name;
            if (tileColors[i] == 1)
                log += "*";
            if (i != 10)
                log += " ";
        }
        Debug.LogFormat("[Syzygy #{0}] One Possible Solution: {1} (tiles marked with * are locked)", moduleId, log);
        //Swap non-locked tiles 100 times to randomize the order for the player
        swapCt = 0;
        oneMore:
        while (swapCt < 100)
        {
            int choice1 = UnityEngine.Random.Range(0, 11);
            int choice2 = UnityEngine.Random.Range(0, 11);
            while (tileColors[choice1] == 1)
                choice1 = UnityEngine.Random.Range(0, 11);
            while (tileColors[choice2] == 1 || choice1 == choice2)
                choice2 = UnityEngine.Random.Range(0, 11);
            Swap(choice1, choice2);
            swapCt++;
        }
        //If the randomization has resulted in a valid order, swap one more time
        if (CheckValidity())
        {
            swapCt = 99;
            goto oneMore;
        }
        UpdateModel();
    }

    void PressTile(KMSelectable pressed)
    {
        int index = Array.IndexOf(buttons, pressed);
        //Prevent a press if the module is solved or the pressed tile is locked
        if (moduleSolved != true && tileColors[index] != 1)
        {
            pressed.AddInteractionPunch(.75f);
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
            //If no tile is selected, the pressed tile is now selected
            if (selected == -1)
            {
                selected = index;
                tiles[index].material = mats[2];
            }
            //If a tile is selected, swap it with the pressed tile and check if the order is valid
            else
            {
                Swap(selected, index);
                UpdateModel();
                if (CheckValidity())
                {
                    moduleSolved = true;
                    GetComponent<KMBombModule>().HandlePass();
                }
                selected = -1;
            }
        }
    }

    //Update the tile visuals
    void UpdateModel()
    {
        for (int i = 0; i < 11; i++)
        {
            tiles[i].material = mats[tileColors[i]];
            symbols[i].sprite = sprites[symbolIndices[i]];
        }
    }

    //Swap the two specified tiles
    void Swap(int index1, int index2)
    {
        int symbol1 = symbolIndices[index1];
        int symbol2 = symbolIndices[index2];
        symbolIndices[index1] = symbol2;
        symbolIndices[index2] = symbol1;
    }

    bool CheckValidity()
    {
        //Check validity of last digit even rules
        if (bomb.GetSerialNumberNumbers().Last() % 2 == 0)
        {
            //Sol
            int solInd = Array.IndexOf(symbolIndices, 0);
            if (solInd == 0)
            {
                if (tileColors[solInd + 1] == 0 && tileColors[solInd] == 0)
                    return false;
            }
            else if (solInd == 10)
            {
                if (tileColors[solInd - 1] == 0 && tileColors[solInd] == 0)
                    return false;
            }
            else if (tileColors[solInd + 1] == 0 && tileColors[solInd - 1] == 0 && tileColors[solInd] == 0)
                return false;
            //Mercurius
            int merInd = Array.IndexOf(symbolIndices, 1);
            int venInd = Array.IndexOf(symbolIndices, 2);
            int jupInd = Array.IndexOf(symbolIndices, 6);
            if (Math.Abs(merInd - venInd) != Math.Abs(merInd - jupInd))
                return false;
            //Venus
            int marInd = Array.IndexOf(symbolIndices, 5);
            if (Math.Abs(venInd - marInd) != 2)
                return false;
            //Terra
            int terInd = Array.IndexOf(symbolIndices, 3);
            if (terInd != 5)
                return false;
            //Luna
            int lunInd = Array.IndexOf(symbolIndices, 4);
            if (Math.Abs(lunInd - terInd) != 1)
                return false;
            //Mars
            int satInd = Array.IndexOf(symbolIndices, 7);
            int pluInd = Array.IndexOf(symbolIndices, 10);
            if (Math.Abs(marInd - satInd) != 1 && Math.Abs(marInd - pluInd) != 1)
                return false;
            //Jupiter
            int nepInd = Array.IndexOf(symbolIndices, 9);
            if (Math.Abs(jupInd - solInd) != 1 && Math.Abs(jupInd - nepInd) != 2)
                return false;
            //Saturnus
            if (satInd < marInd)
                return false;
            else if (satInd == 0)
            {
                if (tileColors[satInd + 1] == 1)
                    return false;
            }
            else if (satInd == 10)
            {
                if (tileColors[satInd - 1] == 1)
                    return false;
            }
            else if (tileColors[satInd + 1] == 1 || tileColors[satInd - 1] == 1)
                return false;
            //Uranus
            int uraInd = Array.IndexOf(symbolIndices, 8);
            if (uraInd > merInd)
                return false;
            //Neptunus is free
            //Pluto
            if (pluInd >= 5)
                return false;
        }
        //Check validity of last digit odd rules
        else
        {
            //Sol
            int solInd = Array.IndexOf(symbolIndices, 0);
            if (solInd != 0)
                return false;
            //Mercurius
            int merInd = Array.IndexOf(symbolIndices, 1);
            int uraInd = Array.IndexOf(symbolIndices, 8);
            int terInd = Array.IndexOf(symbolIndices, 3);
            int lunInd = Array.IndexOf(symbolIndices, 4);
            if (Math.Abs(merInd - uraInd) == 1 || Math.Abs(merInd - terInd) == 1 || Math.Abs(merInd - lunInd) == 1)
                return false;
            //Venus
            int venInd = Array.IndexOf(symbolIndices, 2);
            int pluInd = Array.IndexOf(symbolIndices, 10);
            int satInd = Array.IndexOf(symbolIndices, 7);
            if ((pluInd > satInd && (venInd < satInd || venInd > pluInd)) || (satInd > pluInd && (venInd > satInd || venInd < pluInd)))
                return false;
            //Terra
            int lastLocked = -1;
            for (int i = 0; i < tileColors.Length; i++)
                if (tileColors[i] == 1)
                    lastLocked = i;
            int nepInd = Array.IndexOf(symbolIndices, 9);
            if (terInd > nepInd && terInd < lastLocked)
                return false;
            //Luna
            if (Math.Abs(lunInd - venInd) != 2)
                return false;
            //Mars
            int marInd = Array.IndexOf(symbolIndices, 5);
            if (Math.Abs(marInd - terInd) == 1 || Math.Abs(marInd - solInd) == 1)
                return false;
            //Jupiter
            int jupInd = Array.IndexOf(symbolIndices, 6);
            int[] reversed = symbolIndices.Reverse().ToArray();
            if (jupInd != Array.IndexOf(reversed, 2))
                return false;
            //Saturnus
            if (Math.Abs(satInd - jupInd) != 1 && Math.Abs(satInd - solInd) != 1)
                return false;
            if (Math.Abs(satInd - terInd) == 1 || Math.Abs(satInd - merInd) == 1)
                return false;
            //Uranus
            if (uraInd < terInd || (pluInd > lunInd && (uraInd < lunInd || uraInd > pluInd)) || (lunInd > pluInd && (uraInd > lunInd || uraInd < pluInd)))
                return false;
            //Neptunus
            if (nepInd <= 5)
                return false;
            //Pluto
            if (Math.Abs(pluInd - venInd) != 1 && pluInd < uraInd)
                return false;
        }
        return true;
    }

    //Constantly moves the background texture up and right at a slow rate
    IEnumerator MovingBackground()
    {
        Vector2 offset = new Vector2(0, 0);
        while (true)
        {
            yield return new WaitForSeconds(.01f);
            backMat.SetTextureOffset("_MainTex", offset);
            offset.x -= .0001f;
            offset.y -= .0001f;
        }
    }

    //Twitch Plays support
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} swap <p1> <p2> [Swaps the tiles at positions 'p1' and 'p2'] | Valid positions are 1-11 from left to right | Swaps can be chained using semicolons, for example: !{0} swap 3 5;1 2;8 10";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        //Swap command
        if (command.ToLowerInvariant().StartsWith("swap "))
        {
            //Verify that the command is formatted correctly
            command = command.Substring(5);
            string[] swaps = command.Split(';');
            for (int i = 0; i < swaps.Length; i++)
            {
                string[] swap = swaps[i].Split(' ');
                //Each swap must be formatted as '# #'
                if (swap.Length != 2)
                    yield break;
                if (!swap[0].EqualsAny("1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11") || !swap[1].EqualsAny("1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11"))
                    yield break;
                //Locked tiles cannot be swapped
                if (tileColors[int.Parse(swap[0]) - 1] == 1 || tileColors[int.Parse(swap[1]) - 1] == 1)
                {
                    yield return "sendtochaterror Locked tiles cannot be swapped!";
                    yield break;
                }
            }
            //Command has passed verification, execute the swaps
            yield return null;
            for (int i = 0; i < swaps.Length; i++)
            {
                string[] swap = swaps[i].Split(' ');
                buttons[int.Parse(swap[0]) - 1].OnInteract();
                yield return new WaitForSeconds(.1f);
                buttons[int.Parse(swap[1]) - 1].OnInteract();
                yield return new WaitForSeconds(.1f);
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        //Check each tile to see if they match the intended solution, if one doesn't then swap in the correct one
        for (int i = 0; i < 11; i++)
        {
            if (solverSolution[i] != symbolIndices[i])
            {
                int goalTile = Array.IndexOf(symbolIndices, solverSolution[i]);
                buttons[i].OnInteract();
                yield return new WaitForSeconds(.1f);
                buttons[goalTile].OnInteract();
                yield return new WaitForSeconds(.1f);
            }
            //Stop the solver once it swaps all tiles into a valid solution
            if (moduleSolved)
                yield break;
        }
    }
}