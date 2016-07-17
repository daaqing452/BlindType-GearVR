using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

using StringDouble = System.Collections.Generic.KeyValuePair<string, double>;

public class Recognition
{
    delegate double Prediction(string guess, List<Vector2> pointList);

    const int ALPHABET_SIZE = 26;
    const int LANGUAGE_MODEL_SIZE = 50000;
    const int TOP_K = 25;
    Prediction[] ALGORITHMS = new Prediction[2];

    All all;

    public Dictionary<string, double> languageModel;
    public GaussianPair[] absoluteGaussianPair;
    public GaussianPair[,] relativeGaussianPair;
    Prediction prediction;
    int algorithmIndex = 0;
    
    public Recognition(All all)
    {
        this.all = all;
        LoadLanguageModel();
        LoadAbsoluteKeyboardModel();
        LoadRelativeKeyboardModel();
        ALGORITHMS[0] = Absolute;
        ALGORITHMS[1] = Relative;
    }
    public void ChangeMode(int index = -1)
    {
        algorithmIndex = (algorithmIndex + 1) % ALGORITHMS.Length;
        if (index == -1) index = algorithmIndex;
        prediction = ALGORITHMS[index];
        all.algorithm = (index == 0) ? "Absolute" : "Relative";
    }
    public List<string> Recognize(List<Vector2> pointList)
    {
        PriorityQueue q = new PriorityQueue();
        foreach (StringDouble k in languageModel)
        {
            string s = k.Key;
            if (s.Length != pointList.Count) continue;
            double p = k.Value;
            p *= prediction(s, pointList);
            q.Push(new StringDouble(s, p));
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
        return candidates;
    }

    void LoadLanguageModel()
    {
        languageModel = new Dictionary<string, double>();
        string[] lines = XFileReader.Read("ANC-all-count.txt");
        foreach (string line in lines)
        {
            string[] lineArray = line.Split(' ');
            languageModel[lineArray[0]] = double.Parse(lineArray[1]);
            if (languageModel.Count >= LANGUAGE_MODEL_SIZE) break;
        }
        Debug.Log("Load language model : " + languageModel.Count);
    }
    public void LoadAbsoluteKeyboardModel(bool pre = false, double prexk = 0f, double prexb = 0f, double preyk = 0f, double preyb = 0f)
    {
        absoluteGaussianPair = new GaussianPair[ALPHABET_SIZE];
        string[] lines = XFileReader.Read("Absolute-General-Keyboard.txt");
        string line = lines[0];
        string[] lineArray = line.Split('\t');
        double xk = double.Parse(lineArray[0]);
        double xb = double.Parse(lineArray[1]);
        double xstddev = double.Parse(lineArray[2]);
        double yk = double.Parse(lineArray[3]);
        double yb = double.Parse(lineArray[4]);
        double ystddev = double.Parse(lineArray[5]);
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
    public void LoadRelativeKeyboardModel(bool pre = false, double prexk = 0f, double prexb = 0f, double preyk = 0f, double preyb = 0f)
    {
        relativeGaussianPair = new GaussianPair[ALPHABET_SIZE, ALPHABET_SIZE];
        string[] lines = XFileReader.Read("Relative-General-Keyboard.txt");
        string line = lines[0];
        string[] lineArray = line.Split('\t');
        double xk = double.Parse(lineArray[0]);
        double xb = double.Parse(lineArray[1]);
        double xstddev = double.Parse(lineArray[2]);
        double yk = double.Parse(lineArray[3]);
        double yb = double.Parse(lineArray[4]);
        double ystddev = double.Parse(lineArray[5]);
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

    double Absolute(string s, List<Vector2> pointList)
    {
        double p = 1;
        for (int i = 0; i < pointList.Count; ++i)
        {
            p *= absoluteGaussianPair[s[i] - 'a'].Probability(pointList[i]);
        }
        return p;
    }
    double Relative(string s, List<Vector2> pointList)
    {
        double p = 1;
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
            double bestP = 0;
            char c = ' ';
            for (int j = 0; j < ALPHABET_SIZE; ++j)
            {
                double p = absoluteGaussianPair[j].Probability(pointList[i]);
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
    public double mu;
    public double sigma;
    private double k0;
    private double k1;

    public Gaussian(double mu2, double sigma2)
    {
        mu = mu2;
        sigma = sigma2;
        k0 = 1.0 / Math.Sqrt(2 * Math.Acos(-1)) / sigma;
        k1 = -1.0 / 2 / Math.Pow(sigma, 2);
    }

    public double Probability(double x)
    {
        return k0 * Math.Exp(Math.Pow(x - mu, 2) * k1);
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

    public double Probability(Vector2 p)
    {
        return xD.Probability(p.x) * yD.Probability(p.y);
    }
}

class PriorityQueue
{
    StringDouble[] heap;

    public int Count = 0;

    public PriorityQueue() : this(1) { }

    public PriorityQueue(int capacity)
    {
        heap = new StringDouble[capacity];
    }

    public void Push(StringDouble v)
    {
        if (Count >= heap.Length) Array.Resize(ref heap, Count * 2);
        heap[Count] = v;
        QueueUp(Count++);
    }

    public StringDouble Pop()
    {
        StringDouble v = Top();
        heap[0] = heap[--Count];
        if (Count > 0) QueueDown(0);
        return v;
    }

    public StringDouble Top()
    {
        if (Count > 0) return heap[0];
        throw new InvalidOperationException("Empty priority queue!");
    }

    void QueueUp(int n)
    {
        StringDouble v = heap[n];
        for (var n2 = n / 2; n > 0; n = n2, n2 /= 2)
        {
            if (heap[n2].Value <= v.Value) break;
            heap[n] = heap[n2];
        }
        heap[n] = v;
    }

    void QueueDown(int n)
    {
        StringDouble v = heap[n];
        for (var n2 = n * 2; n2 < Count; n = n2, n2 *= 2)
        {
            if (n2 + 1 < Count && heap[n2 + 1].Value < heap[n2].Value) n2++;
            if (v.Value <= heap[n2].Value) break;
            heap[n] = heap[n2];
        }
        heap[n] = v;
    }
}