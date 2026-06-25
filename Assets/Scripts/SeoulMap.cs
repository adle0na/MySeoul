using System.Collections.Generic;

namespace SeoulLast
{
    // 서울 행정구역 대략 배치 (정규화 좌표 x:0좌→1우, y:0상→1하)
    public struct District
    {
        public string Name;
        public float Nx, Ny;
        public District(string n, float x, float y) { Name = n; Nx = x; Ny = y; }
    }

    public static class SeoulMap
    {
        public static readonly List<District> Districts = new List<District>
        {
            new District("도봉구",   0.60f, 0.10f),
            new District("노원구",   0.74f, 0.17f),
            new District("강북구",   0.54f, 0.20f),
            new District("은평구",   0.36f, 0.27f),
            new District("성북구",   0.56f, 0.30f),
            new District("중랑구",   0.74f, 0.32f),
            new District("서대문구", 0.39f, 0.38f),
            new District("종로구",   0.49f, 0.37f),
            new District("동대문구", 0.64f, 0.37f),
            new District("강서구",   0.12f, 0.43f),
            new District("마포구",   0.31f, 0.46f),
            new District("중구",     0.52f, 0.44f),
            new District("성동구",   0.63f, 0.44f),
            new District("광진구",   0.74f, 0.45f),
            new District("강동구",   0.87f, 0.42f),
            new District("양천구",   0.16f, 0.55f),
            new District("용산구",   0.50f, 0.52f),
            new District("영등포구", 0.32f, 0.55f),
            new District("동작구",   0.44f, 0.58f),
            new District("구로구",   0.19f, 0.64f),
            new District("관악구",   0.42f, 0.71f),
            new District("금천구",   0.27f, 0.73f),
            new District("서초구",   0.56f, 0.64f),
            new District("강남구",   0.67f, 0.62f),
            new District("송파구",   0.80f, 0.57f),
        };
    }
}
