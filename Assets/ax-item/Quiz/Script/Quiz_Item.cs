
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Quiz_Item : UdonSharpBehaviour
{
    [Tooltip("選択肢をシャッフルするか")]
    public bool isShuffle = true;
    [TextArea]
    public string title;

    public AudioClip mainSound;
    [Header("最大4つまで指定できます")]
    [TextArea]
    public string[] select;
    public int[] answer_index;
    public AudioClip[] answer_sounds;
    [TextArea]
    public string comment;
    public float score;
    public void gen(){
    }
    [TextArea, Header("以下はメモエリアです ギミック上では使用しません")]
    public string memo;
}
