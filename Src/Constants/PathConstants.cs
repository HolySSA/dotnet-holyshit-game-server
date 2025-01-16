namespace Constants;

public static class PathConstants
{
  public static readonly string PROJECT_ROOT = Directory.GetCurrentDirectory(); // 프로젝트 루트 경로

  // Assets 디렉토리 관련 경로
  public static class Assets
  {
    public static readonly string ASSETS_REL_PATH = "Assets"; // Assets 폴더 상대 경로
    public static readonly string ASSETS_ABS_PATH = Path.Combine(PROJECT_ROOT, ASSETS_REL_PATH); // Assets 폴더 절대 경로

    // 데이터 파일 이름
    public static class DataFiles
    {
      public static readonly string MONSTER_INFO = "MonsterInfo.json";
    }
  }

  // 파일 확장자 정의
  public static class FileExtension
  {
    public static readonly string JSON = ".json"; // JSON 파일 확장자
  }
}