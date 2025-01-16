using System.Text.Json;

namespace Utils.Loader;

public class JsonFileLoader : BaseFileLoader
{
  private readonly JsonSerializerOptions _options; // JSON 변환 옵션 저장

  // JSON 변환 옵션 초기화 - 인스턴스 생성 시
  public JsonFileLoader()
  {
    _options = new JsonSerializerOptions
    {
      AllowTrailingCommas = true, // 데이터에서 쉼표(,) 허용
      MaxDepth = 64, // JSON 파싱 최대 깊이 64
      PropertyNameCaseInsensitive = true, // 이름 대소문자 구분 X
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // 속성 이름 camelCase 처리
      ReadCommentHandling = JsonCommentHandling.Skip // 주석 무시
    };
  }

  // JSON 파일 읽어 제네릭 타입으로 변환
  protected override T ReadFile<T>(string filePath) where T : class
  {
    string jsonContent = File.ReadAllText(filePath);  // 지정 경로에서 JSON 파일 내용 문자열로 읽기
    var result = JsonSerializer.Deserialize<T>(jsonContent, _options); // 문자열을 지정된 타입으로 변환

    if (result == null)
      throw new InvalidDataException($"JSON 파일 deserialize 실패: {filePath}");

    // 디시리얼라이즈된 객체 반환
    return result;
  }
}
