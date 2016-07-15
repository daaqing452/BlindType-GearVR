﻿using System.Collections.Generic;
using UnityEngine;

public class Adaption
{
    const int ADAPT_CNT = 6;

    int sentenceCnt;
    List<Vector2> absX, absY;
    List<Vector2> relX, relY;
    Recognition r;

    public Adaption(Recognition r)
    {
        sentenceCnt = 0;
        absX = new List<Vector2>();
        absY = new List<Vector2>();
        relX = new List<Vector2>();
        relY = new List<Vector2>();
        this.r = r;
    }

    public void AddData(List<Vector2[]> inputedPointsAll, List<string> inputedWords)
    {
        sentenceCnt++;
        if (sentenceCnt > ADAPT_CNT) return;
        for (int i = 0; i < inputedPointsAll.Count; i++)
        {
            Vector2[] inputedPoints = inputedPointsAll[i];
            for (int j = 0; j < inputedPoints.Length; j++)
            {
                Vector2 s = Recognition.StandardPosition(inputedWords[i][j]);
                absX.Add(new Vector2(s.x, inputedPoints[j].x));
                absY.Add(new Vector2(s.y, inputedPoints[j].y));
                if (j > 0)
                {
                    Vector2 v3 = inputedPoints[j] - inputedPoints[j - 1];
                    Vector2 s3 = s - Recognition.StandardPosition(inputedWords[i][j - 1]);
                    relX.Add(new Vector2(s3.x, v3.x));
                    relY.Add(new Vector2(s3.y, v3.y));
                }
            }
        }
        if (sentenceCnt == ADAPT_CNT) ApplyResult();
    }

    static float[,] Product(float[,] A, float[,] B, int M, int N, int P)
    {
        float[,] C = new float[M, P];
        for (int i = 0; i < M; i++)
            for (int j = 0; j < P; j++)
                for (int k = 0; k < N; k++)
                    C[i, j] += A[i, k] * B[k, j];
        return C;
    }

    static float[,] Inv2x2(float[,] A)
    {
        float[,] Ainv = new float[2, 2];
        float l = 1.0f / (A[0, 0] * A[1, 1] - A[0, 1] * A[1, 0]);
        Ainv[0, 0] = A[1, 1] * l;
        Ainv[0, 1] = A[0, 1] * -l;
        Ainv[1, 0] = A[1, 0] * -l;
        Ainv[1, 1] = A[0, 0] * l;
        return Ainv;
    }

    Vector2 LeastSquareMethod(List<Vector2> Z)
    {
        int N = Z.Count;
        float[,] A = new float[N, 2];
        float[,] At = new float[2, N];
        float[,] B = new float[N, 1];
        for (int i = 0; i < N; i++)
        {
            A[i, 0] = At[0, i] = Z[i].x;
            A[i, 1] = At[1, i] = 1;
            B[i, 0] = Z[i].y;
        }
        float[,] AtA = Product(At, A, 2, N, 2);
        float[,] AtAinv = Inv2x2(AtA);
        float[,] AtAinvAt = Product(AtAinv, At, 2, 2, N);
        float[,] X = Product(AtAinvAt, B, 2, N, 1);
        return new Vector2(X[0, 0], X[1, 0]);
    }

    void ApplyResult()
    {
        Vector2 absXkb = LeastSquareMethod(absX);
        Vector2 absYkb = LeastSquareMethod(absY);
        Vector2 relXkb = LeastSquareMethod(relX);
        Vector2 relYkb = LeastSquareMethod(relY);
        Debug.Log("abs param: " + absXkb.x + " " + absXkb.y + " " + absYkb.x + " " + absYkb.y);
        Debug.Log("rel param: " + relXkb.x + " " + relXkb.y + " " + relYkb.x + " " + relYkb.y);
        r.LoadAbsoluteKeyboardModel(true, absXkb.x, absXkb.y, absYkb.x, absYkb.y);
        r.LoadRelativeKeyboardModel(true, relXkb.x, relXkb.y, relYkb.x, relYkb.y);
    }
}
