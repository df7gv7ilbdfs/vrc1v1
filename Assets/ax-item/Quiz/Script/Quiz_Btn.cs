
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public class Quiz_Btn : UdonSharpBehaviour
{

    public string text;
    public int num;
    public UdonBehaviour motherboard;

    void Start()
    {
    }

    public void trigger()
    {
        int[] ans = (int[])motherboard.GetProgramVariable("current_select");
        for (int i = 0; i < ans.Length; i++)
        {
            if (ans[i] == num)
            {
                motherboard.SendCustomEvent("correct");
                return;
            }
        }
        motherboard.SendCustomEvent("incorrect");

    }

    public void playSound()
    {
        AudioClip[] clips = (AudioClip[])motherboard.GetProgramVariable("current_answerSounds");
        AudioSource audio = (AudioSource)motherboard.GetProgramVariable("question_audio");
        if (
            Utilities.IsValid(clips[num]) &&
            clips.Length > num &&
            Utilities.IsValid(audio)
            )
        {
            audio.Stop();
            audio.PlayOneShot(clips[num]);
        }
    }
}
