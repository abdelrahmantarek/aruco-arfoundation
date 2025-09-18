# دليل تحليل أداء تطبيق AR مع ArUco

## نظرة عامة

تم إضافة مجموعة من الأدوات لتحليل وتحسين أداء تطبيق AR الذي يستخدم ArUco markers. هذه الأدوات تساعد في:

1. **تحديد مصادر اللاج** في معالجة الكاميرا
2. **قياس الأداء** بدقة
3. **تحسين الإعدادات** تلقائياً
4. **مراقبة الأداء** في الوقت الفعلي

## الأدوات المضافة

### 1. PerformanceAnalyzer
**الملف**: `Assets/MobileARTemplateAssets/Scripts/Utils/PerformanceAnalyzer.cs`

**الوظيفة**: قياس وقت تنفيذ العمليات المختلفة
- قياس وقت اكتشاف ArUco
- قياس وقت تقدير الوضعية
- قياس وقت تحديث الكائنات
- عرض تحذيرات عند تجاوز الحدود المقبولة

### 2. PerformanceTestRunner
**الملف**: `Assets/MobileARTemplateAssets/Scripts/Utils/PerformanceTestRunner.cs`

**الوظيفة**: تشغيل اختبارات أداء شاملة
- اختبار لمدة 30 ثانية
- تحليل تلقائي للنتائج
- تحديد الاختناقات (Bottlenecks)
- توصيات للتحسين

### 3. PerformanceUI
**الملف**: `Assets/MobileARTemplateAssets/Scripts/Utils/PerformanceUI.cs`

**الوظيفة**: واجهة مستخدم لمراقبة الأداء
- عرض مقاييس الأداء في الوقت الفعلي
- تحكم في frameSkipCount
- زر لبدء اختبار الأداء

### 4. PerformanceOptimizer
**الملف**: `Assets/MobileARTemplateAssets/Scripts/Utils/PerformanceOptimizer.cs`

**الوظيفة**: تحسين الأداء تلقائياً
- كشف نوع الجهاز تلقائياً
- تطبيق إعدادات محسنة
- تقليل دقة المعالجة
- تحسين معاملات ArUco

## كيفية الاستخدام

### الخطوة 1: إضافة المكونات

1. افتح المشهد الرئيسي للتطبيق
2. اختر GameObject الذي يحتوي على `ARFoundationCameraArUcoExample`
3. أضف المكونات التالية:

```csharp
// في Unity Inspector أو عبر الكود
gameObject.AddComponent<PerformanceAnalyzer>();
gameObject.AddComponent<PerformanceTestRunner>();
gameObject.AddComponent<PerformanceUI>();
gameObject.AddComponent<PerformanceOptimizer>();
```

### الخطوة 2: تشغيل التطبيق

1. شغل التطبيق على الجهاز المستهدف
2. انتظر 3 ثوانِ للتهيئة
3. سيبدأ اختبار الأداء تلقائياً

### الخطوة 3: مراقبة النتائج

#### في Unity Console:
```
=== PERFORMANCE TEST RESULTS ===
Total frame processing time: 45.23ms
ArUco Detection: 28.15ms (62.3% of total) [WARNING]
Pose Estimation: 12.08ms (26.7% of total)
Texture Update: 3.45ms (7.6% of total)
Image Undistortion: 1.55ms (3.4% of total)

=== BOTTLENECK ANALYSIS ===
[WARNING] ArUco Detection is taking too much time! Consider optimizing detection parameters.
[CRITICAL] Frame processing time (45.23ms) exceeds 30 FPS threshold (33.33ms)!
```

#### على الشاشة:
- عرض FPS الحالي
- أوقات المعالجة لكل عملية
- حالة Frame Skip
- أزرار للتحكم

## تفسير النتائج

### مستويات الأداء

#### 🟢 جيد (أخضر)
- وقت المعالجة < 16.67ms (60 FPS)
- لا حاجة لتحسينات

#### 🟡 تحذير (أصفر)
- وقت المعالجة 16.67-33.33ms (30-60 FPS)
- يحتاج تحسينات بسيطة

#### 🔴 حرج (أحمر)
- وقت المعالجة > 33.33ms (< 30 FPS)
- يحتاج تحسينات فورية

### مصادر اللاج الشائعة

1. **ArUco Detection (60%+)**
   - المصدر الرئيسي للاج
   - الحل: تحسين معاملات الكشف، تقليل دقة الصورة

2. **Pose Estimation (25%+)**
   - حسابات رياضية معقدة
   - الحل: تقليل عدد markers، تحسين تكرار المعالجة

3. **Image Processing (10%+)**
   - تحويل ألوان وتصحيح تشويه
   - الحل: تعطيل undistortion، تحسين تحويل الألوان

## الحلول السريعة

### للحصول على تحسن فوري:

1. **زيادة Frame Skip Count**:
```csharp
// في ARFoundationCameraArUcoExample
arExample.UpdateFrameSkipCount(7); // بدلاً من 3
```

2. **تقليل دقة المعالجة**:
```csharp
// في PerformanceOptimizer
processingScale = 0.3f; // معالجة 30% من الدقة الأصلية
```

3. **تعطيل Undistortion**:
```csharp
// في ProcessFrame
enableUndistortion = false;
```

### للتحسين المتقدم:

1. **استخدام ROI (Region of Interest)**:
```csharp
// معالجة منطقة محددة فقط من الصورة
enableROI = true;
roiRect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);
```

2. **تحسين معاملات ArUco**:
```csharp
// معاملات محسنة للأجهزة الضعيفة
detectorParams.set_adaptiveThreshWinSizeMin(5);
detectorParams.set_adaptiveThreshWinSizeMax(15);
```

## إعدادات الأجهزة المختلفة

### الأجهزة الضعيفة (< 3GB RAM):
```csharp
frameSkipCount = 7;
processingScale = 0.3f;
enableROI = true;
enableUndistortion = false;
```

### الأجهزة المتوسطة (3-6GB RAM):
```csharp
frameSkipCount = 4;
processingScale = 0.5f;
enableROI = false;
enableUndistortion = true;
```

### الأجهزة القوية (> 6GB RAM):
```csharp
frameSkipCount = 2;
processingScale = 0.7f;
enableROI = false;
enableUndistortion = true;
```

## استكشاف الأخطاء

### المشكلة: لا تظهر مقاييس الأداء
**الحل**: تأكد من إضافة PerformanceAnalyzer إلى نفس GameObject

### المشكلة: اختبار الأداء لا يبدأ
**الحل**: تأكد من وجود ARFoundationCameraArUcoExample في المشهد

### المشكلة: النتائج غير دقيقة
**الحل**: شغل الاختبار على الجهاز المستهدف، ليس في Unity Editor

## الخلاصة

هذه الأدوات تساعدك في:
1. **تحديد** مصدر اللاج بدقة
2. **قياس** تأثير كل تحسين
3. **تحسين** الأداء تلقائياً
4. **مراقبة** الأداء باستمرار

**النصيحة الذهبية**: ابدأ بزيادة frameSkipCount إلى 5-7 للحصول على تحسن فوري، ثم طبق التحسينات الأخرى تدريجياً.
