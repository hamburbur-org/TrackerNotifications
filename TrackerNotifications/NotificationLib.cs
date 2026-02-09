using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.UI;

namespace TrackerNotifications;

// all of ts is skidded from hamburbur because i will NOT be writing a fuckass notificationlib again AJSFÖLKhsdflkjasdcvplokaöufgdJFGVBKDk
public class NotificationLib : MonoBehaviour
{
    private readonly List<NotificationEntry> notifications = [];
    private          Text                    notificationText;
    public static    NotificationLib         Instance { get; private set; }

    private void Start()
    {
        GameObject notificationCanvasPrefab = Plugin.NotificationBundle.LoadAsset<GameObject>("NotificationLibCanvas");
        GameObject notificationCanvas = Instantiate(notificationCanvasPrefab, GTPlayer.Instance.headCollider.transform);
        Destroy(notificationCanvasPrefab);
        notificationCanvas.name = "NotificationLibCanvas";

        notificationCanvas.transform.localPosition = new Vector3(-0.25f, -0.2f, 1f);
        notificationCanvas.transform.localRotation = Quaternion.Euler(20f, -20f, 0f);

        notificationText      = notificationCanvas.GetComponentInChildren<Text>();
        notificationText.text = "";

        Instance = this;
    }

    public void SendNotification(string notification, float duration)
    {
        if (string.IsNullOrWhiteSpace(notification))
            return;

        notification = InsertNewlinesWithRichText(NormaliseString(notification), 50);

        NotificationEntry entry = new(notification, duration);
        notifications.Add(entry);
        RefreshText();

        StartCoroutine(RemoveNotification(entry));
    }

    private IEnumerator RemoveNotification(NotificationEntry entry)
    {
        yield return new WaitForSeconds(entry.Duration);
        notifications.Remove(entry);
        RefreshText();
    }

    private void RefreshText()
    {
        string combined = string.Join("\n", notifications.ConvertAll(n => n.Text));
        notificationText.text = combined;
    }

    private static string InsertNewlinesWithRichText(string input, int interval)
    {
        if (string.IsNullOrEmpty(input) || interval <= 0)
            return input;

        StringBuilder output                       = new();
        int           visibleCount                 = 0;
        int           lastWhitespaceIndex          = -1;
        int           outputLengthAtLastWhitespace = -1;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '<')
            {
                int tagEnd = input.IndexOf('>', i);
                if (tagEnd == -1)
                {
                    output.Append(c);

                    continue;
                }

                output.Append(input.AsSpan(i, tagEnd - i + 1));
                i = tagEnd;

                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                lastWhitespaceIndex          = i;
                outputLengthAtLastWhitespace = output.Length;
            }

            output.Append(c);
            visibleCount++;

            if (visibleCount < interval)
                continue;

            if (outputLengthAtLastWhitespace != -1)
            {
                output[outputLengthAtLastWhitespace] = '\n';
                visibleCount                         = i - lastWhitespaceIndex;
                lastWhitespaceIndex                  = -1;
                outputLengthAtLastWhitespace         = -1;
            }
            else
            {
                output.Append('\n');
                visibleCount = 0;
            }
        }

        return output.ToString();
    }

    private static string NormaliseString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = Regex.Replace(text, @"<size\s*=\s*[^>]+>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</size>",            "", RegexOptions.IgnoreCase);

        return text;
    }

    private class NotificationEntry(string text, float duration)
    {
        public readonly float  Duration = duration;
        public readonly string Text     = text;
    }
}