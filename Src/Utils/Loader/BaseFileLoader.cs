using Constants;

namespace Utils.Loader;

public abstract class BaseFileLoader
{
  // 지정된 디렉토리에서 특정 확장자를 가진 모든 파일 경로 검색
  // directoryPath: 검색할 디렉토리 경로 / fileExtension: 검색할 파일 확장자
  public string[] GetFilePaths(string directoryPath, string fileExtension)
  {
    if (string.IsNullOrEmpty(directoryPath))
      throw new ArgumentNullException(nameof(directoryPath));

    if (string.IsNullOrEmpty(fileExtension))
      throw new ArgumentNullException(nameof(fileExtension));

    return Directory.GetFiles(directoryPath, $"*{fileExtension}"); // 파일 경로 배열
  }

  // 단일 파일을 지정된 타입으로 변환 - 구현체에서 실제 파일 읽기 로직을 정의.
  protected abstract T ReadFile<T>(string filePath) where T : class;

  // 여러 파일을 읽어 지정된 타입의 배열로 변환.
  // T: 변환할 데이터 타입 / filePaths: 읽을 파일 경로들 배열
  public T[] LoadFiles<T>(string[] filePaths) where T : class
  {
    if (filePaths == null || filePaths.Length == 0)
      throw new ArgumentException("파일 경로 배열이 비어있습니다.", nameof(filePaths));

    return filePaths.Select(path => ReadFile<T>(path)).ToArray();
  }

  public T[] LoadAllFiles<T>(string directoryPath, string fileExtension) where T : class
  {
    var filePaths = GetFilePaths(directoryPath, fileExtension);
    // 변환된 데이터 객체들의 배열
    return LoadFiles<T>(filePaths);
  }

  // Assets 디렉토리에서 지정된 파일을 읽어 변환
  // T: 변환할 데이터 타입 / fileName: 파일 이름(확장자 포함)
  public T LoadFromAssets<T>(string fileName) where T : class
  {
    if (string.IsNullOrEmpty(fileName))
      throw new ArgumentNullException(nameof(fileName));

    string fullPath = Path.Combine(PathConstants.Assets.ASSETS_REL_PATH, fileName);
    // return 변환된 데이터 객체
    return ReadFile<T>(fullPath);
  }
}
