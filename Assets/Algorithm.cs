using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

using StringFloat = System.Collections.Generic.KeyValuePair<string, float>;

public class Recognition
{
    delegate float Prediction(string guess, List<Vector2> pointList);

    const int ALPHABET_SIZE = 26;
    const int LANGUAGE_MODEL_SIZE = 50000;
    const int TOP_K = 25;
    Prediction[] ALGORITHMS = new Prediction[2];

    public Dictionary<string, float> languageModel;
    public GaussianPair[] absoluteGaussianPair;
    public GaussianPair[,] relativeGaussianPair;
    Prediction prediction;
    int algorithmIndex = 0;
    
    public Recognition()
    {
        LoadLanguageModel();
        LoadAbsoluteKeyboardModel();
        LoadRelativeKeyboardModel();
        ALGORITHMS[0] = Absolute;
        ALGORITHMS[1] = Relative;
        ChangeMode(1);
    }
    public void ChangeMode(int index = -1)
    {
        if (index == -1) index = (algorithmIndex + 1) % ALGORITHMS.Length;
        prediction = ALGORITHMS[index];
    }
    public string[] Recognize(List<Vector2> pointList)
    {
        PriorityQueue q = new PriorityQueue();
        foreach (StringFloat k in languageModel)
        {
            string s = k.Key;
            if (s.Length != pointList.Count) continue;
            float p = k.Value;
            p *= prediction(s, pointList);
            q.Push(new StringFloat(s, p));
            if (q.Count > TOP_K) q.Pop();
        }
        List<string> candidates = new List<string>();
        candidates.Add(SimilarSequence(pointList));
        while (q.Count > 0)
        {
            candidates.Add(q.Pop().Key);
        }
        candidates.Reverse();
        Ordering(candidates);
        return candidates.ToArray();
    }

    void LoadLanguageModel()
    {
        languageModel = new Dictionary<string, float>();
        string[] lines = XFileReader.Read("ANC-all-count.txt");
        foreach (string line in lines)
        {
            string[] lineArray = line.Split(' ');
            languageModel[lineArray[0]] = float.Parse(lineArray[1]);
            if (languageModel.Count >= LANGUAGE_MODEL_SIZE) break;
        }
        Debug.Log("Load language model : " + languageModel.Count);
    }
    public void LoadAbsoluteKeyboardModel(bool pre = false, float prexk = 0f, float prexb = 0f, float preyk = 0f, float preyb = 0f)
    {
        absoluteGaussianPair = new GaussianPair[ALPHABET_SIZE];
        string[] lines = XFileReader.Read("Absolute-General-Keyboard.txt");
        string line = lines[0];
        string[] lineArray = line.Split('\t');
        float xk = float.Parse(lineArray[0]);
        float xb = float.Parse(lineArray[1]);
        float xstddev = float.Parse(lineArray[2]);
        float yk = float.Parse(lineArray[3]);
        float yb = float.Parse(lineArray[4]);
        float ystddev = float.Parse(lineArray[5]);
        if (pre)
        {
            xk = prexk;
            xb = prexb;
            yk = preyk;
            yb = preyb;
        }
        for (int i = 0; i < ALPHABET_SIZE; ++i)
        {
            Vector2 p = StandardPosition((char)(i + 'a'));
            absoluteGaussianPair[i] = new GaussianPair(new Gaussian(p.x * xk + xb, xstddev), new Gaussian(p.y * yk + yb, ystddev));
        }
        Debug.Log("Load absolute keyboard GaussianPair : " + absoluteGaussianPair.Length);
    }
    public void LoadRelativeKeyboardModel(bool pre = false, float prexk = 0f, float prexb = 0f, float preyk = 0f, float preyb = 0f)
    {
        relativeGaussianPair = new GaussianPair[ALPHABET_SIZE, ALPHABET_SIZE];
        string[] lines = XFileReader.Read("Relative-General-Keyboard.txt");
        string line = lines[0];
        string[] lineArray = line.Split('\t');
        float xk = float.Parse(lineArray[0]);
        float xb = float.Parse(lineArray[1]);
        float xstddev = float.Parse(lineArray[2]);
        float yk = float.Parse(lineArray[3]);
        float yb = float.Parse(lineArray[4]);
        float ystddev = float.Parse(lineArray[5]);
        if (pre)
        {
            xk = prexk;
            xb = prexb;
            yk = preyk;
            yb = preyb;
        }
        for (int i = 0; i < ALPHABET_SIZE; ++i)
            for (int j = 0; j < ALPHABET_SIZE; ++j)
            {
                Vector2 p = StandardPosition((char)(i + 'a')) - StandardPosition((char)(j + 'a'));
                relativeGaussianPair[i, j] = new GaussianPair(new Gaussian(p.x * xk + xb, xstddev), new Gaussian(p.y * yk + yb, ystddev));
            }
        Debug.Log("Load relative keyboard GaussianPair : " + relativeGaussianPair.Length);
    }

    float Absolute(string s, List<Vector2> pointList)
    {
        float p = 1;
        for (int i = 0; i < pointList.Count; ++i)
        {
            p *= absoluteGaussianPair[s[i] - 'a'].Probability(pointList[i]);
        }
        return p;
    }
    float Relative(string s, List<Vector2> pointList)
    {
        float p = 1;
        p *= absoluteGaussianPair[s[0] - 'a'].Probability(pointList[0]);
        for (int i = 1; i < pointList.Count; ++i)
        {
            p *= relativeGaussianPair[s[i] - 'a', s[i - 1] - 'a'].Probability(pointList[i] - pointList[i - 1]);
        }
        return p;
    }

    public static Vector2 StandardPosition(char c)
    {
        string[] standardKeyboard = new string[3] { "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        float[] xBias = new float[3] { 0, 0.25f, 0.75f };
        for (int i = 0; i < 3; ++i)
            for (int j = 0; j < standardKeyboard[i].Length; ++j)
                if (standardKeyboard[i][j] == c)
                {
                    return new Vector2(xBias[i] + j, i);
                }
        return new Vector2(-1, -1);
    }
    string SimilarSequence(List<Vector2> pointList)
    {
        string similarSequence = "";
        for (int i = 0; i < pointList.Count; ++i)
        {
            float bestP = 0;
            char c = ' ';
            for (int j = 0; j < ALPHABET_SIZE; ++j)
            {
                float p = absoluteGaussianPair[j].Probability(pointList[i]);
                if (p > bestP)
                {
                    bestP = p;
                    c = (char)('a' + j);
                }
            }
            similarSequence += c;
        }
        return similarSequence;
    }
    void Ordering(List<string> a)
    {
        int lastIndex = (a.Count > TOP_K) ? a.Count - 1 : a.Count;
        if (lastIndex > 5)
        {
            a.Sort(5, lastIndex - 5, Comparer<string>.Default);
        }
    }
}

public class Gaussian
{
    public float mu;
    public float sigma;
    private float k0;
    private float k1;

    public Gaussian(float mu2, float sigma2)
    {
        mu = mu2;
        sigma = sigma2;
        k0 = (float)(1.0 / Math.Sqrt(2 * Math.Acos(-1)) / sigma);
        k1 = (float)(-1.0 / 2 / Math.Pow(sigma, 2));
    }

    public float Probability(float x)
    {
        return (float)(k0 * Math.Exp(Math.Pow(x - mu, 2) * k1));
    }
}

public class GaussianPair
{
    public Gaussian xD;
    public Gaussian yD;

    public GaussianPair(Gaussian xD2, Gaussian yD2)
    {
        xD = xD2;
        yD = yD2;
    }

    public float Probability(Vector2 p)
    {
        return xD.Probability(p.x) * yD.Probability(p.y);
    }
}

class PriorityQueue
{
    StringFloat[] heap;

    public int Count = 0;

    public PriorityQueue() : this(1) { }

    public PriorityQueue(int capacity)
    {
        heap = new StringFloat[capacity];
    }

    public void Push(StringFloat v)
    {
        if (Count >= heap.Length) Array.Resize(ref heap, Count * 2);
        heap[Count] = v;
        QueueUp(Count++);
    }

    public StringFloat Pop()
    {
        StringFloat v = Top();
        heap[0] = heap[--Count];
        if (Count > 0) QueueDown(0);
        return v;
    }

    public StringFloat Top()
    {
        if (Count > 0) return heap[0];
        throw new InvalidOperationException("Empty priority queue!");
    }

    void QueueUp(int n)
    {
        StringFloat v = heap[n];
        for (var n2 = n / 2; n > 0; n = n2, n2 /= 2)
        {
            if (heap[n2].Value <= v.Value) break;
            heap[n] = heap[n2];
        }
        heap[n] = v;
    }

    void QueueDown(int n)
    {
        StringFloat v = heap[n];
        for (var n2 = n * 2; n2 < Count; n = n2, n2 *= 2)
        {
            if (n2 + 1 < Count && heap[n2 + 1].Value < heap[n2].Value) n2++;
            if (v.Value <= heap[n2].Value) break;
            heap[n] = heap[n2];
        }
        heap[n] = v;
    }
}