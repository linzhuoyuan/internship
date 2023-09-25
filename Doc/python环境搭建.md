# Python环境搭建

## 一、Python安装
下载python3.7版本，可以选择Anaconda3下载
Anaconda：https://repo.anaconda.com/archive/Anaconda3-2020.02-Windows-x86_64.exe

## 二、配置Python环境变量
假设我们的Anaconda安装目录是C:\Python\Anaconda3

### 添加到Path环境
C:\Python\Anaconda3;C:\Python\Anaconda3\Scripts;C:\Python\Anaconda3\Library\bin

### 新建环境变量PYTHONHOME
变量内容 C:\Python\Anaconda3

### 新建环境变量PYTHONPATH
变量内容 C:\Python\Anaconda3;C:\Python\Anaconda3\Lib;C:\Python\Anaconda3\Lib\site-packages


## 三、quantconnect 运行Python
配置config.json，文件目录地址：根目录\QuantConnect.Lean.Launcher\config.json

```json
"algorithm-type-name": "BasicTemplateAlgorithm", //策略类名称
"algorithm-language": "Python",
"algorithm-location": "../../../Algorithm.Python/BasicTemplateAlgorithm.py", //策略文件路径
```