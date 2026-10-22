using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class NPCAppearance : MonoBehaviour
{
    [System.Flags]
    public enum ClothingPart
    {
        None = 0,
        WhiteShirt = 1 << 0,
        BlackShirt = 1 << 1,
        BlueShirt = 1 << 2,
        GreenShirt = 1 << 3,
        RedShirt = 1 << 4,
        BlackPants = 1 << 5,
        BluePants = 1 << 6
    }
    [Header("Child Sprite References")]
    public GameObject whiteShirt;
    public GameObject blackShirt;
    public GameObject blueShirt;
    public GameObject greenShirt;
    public GameObject redShirt;
    public GameObject blackPants;
    public GameObject bluePants;

    public ClothingPart currentClothing { get; private set;}

 public void SetupAppearance(ClothingPart clothing)
    {
        currentClothing = clothing;
        if(whiteShirt) whiteShirt.SetActive(false);
        if(blackShirt) blackShirt.SetActive(false);
        if(greenShirt) greenShirt.SetActive(false);
        if(blueShirt) blueShirt.SetActive(false);
        if(redShirt) redShirt.SetActive(false);
        if(blackPants) blackPants.SetActive(false);
        if(bluePants) bluePants.SetActive(false);

        if((clothing & ClothingPart.RedShirt) != 0 && redShirt) redShirt .SetActive(true);
        if((clothing & ClothingPart.BlueShirt) != 0 && blueShirt) blueShirt.SetActive(true);
        if((clothing & ClothingPart.GreenShirt) != 0 && greenShirt) greenShirt.SetActive(true);
        if((clothing & ClothingPart.BlackShirt) != 0 && blackShirt) blackShirt.SetActive(true);
        if((clothing & ClothingPart.WhiteShirt) != 0 && whiteShirt) whiteShirt.SetActive(true);
        if((clothing & ClothingPart.BlackPants) != 0 && blackPants) blackPants.SetActive(true);
        if((clothing & ClothingPart.BluePants) != 0 && bluePants) bluePants.SetActive(true);
    }
}
