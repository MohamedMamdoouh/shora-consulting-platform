export type TopicAccent = 'purple' | 'green' | 'orange' | 'pink' | 'sky';

export interface Topic {
  id: string;
  title: string;
  shortDescription: string;
  longDescription: string;
  accent: TopicAccent;
}

export const CONSULTATION_TOPICS: Topic[] = [
  {
    id: 'communication',
    title: 'مش عارف تتكلموا من غير خناقة؟',
    shortDescription: 'أساعدك تفهم وتوصل اللي جواك بشكل أفضل.',
    longDescription:
      'لو كل محادثة بتتحول لخناقة، أساعدك تفهم اللي جواك وتوصله بشكل أوضح — من غير تصعيد أو اتهامات.',
    accent: 'purple',
  },
  {
    id: 'trust',
    title: 'الثقة اتكسرت؟',
    shortDescription: 'نفهم اللي حصل ونشوف إزاي ممكن ترجعوا الأمان.',
    longDescription:
      'بعد خذلان أو شك، بنفهم اللي حصل الأول — وبعدين نشوف سوا خطوات واقعية ترجعوا بيها الأمان والاطمئنان.',
    accent: 'green',
  },
  {
    id: 'dating-confidence',
    title: 'خايف تاخد خطوة في علاقة جديدة؟',
    shortDescription: 'نتعامل مع القلق والخوف من الرفض.',
    longDescription:
      'القلق والخوف من الرفض طبيعيين في بداية علاقة جديدة. نتعامل معهم سوا ونشتغل على خطوات تخليك أهدأ وأوضح.',
    accent: 'pink',
  },
  {
    id: 'long-distance',
    title: 'المسافة مأثرة على علاقتكم؟',
    shortDescription: 'نلاقي طرق تحافظوا بيها على القرب رغم البعد.',
    longDescription:
      'المسافة واختلاف التوقيت بيأثروا على أي علاقة. نلاقي طرق عملية تحافظوا بيها على القرب والتواصل رغم البعد.',
    accent: 'sky',
  },
];

export const HOW_IT_WORKS_STEPS = [
  {
    title: 'احجز موعدًا',
    description: 'اختر الوقت المناسب لك من المواعيد المتاحة.',
  },
  {
    title: 'حول الرسوم وارفع الإيصال',
    description: 'حول عبر فودافون كاش أو إنستا باي ثم ارفع إيصال التحويل.',
  },
  {
    title: 'تأكيد الحجز',
    description: 'ستتم مراجعة إيصال التحويل وتأكيد موعدك.',
  },
  {
    title: 'استلم جلستك',
    description: 'ستجرى الجلسة في الموعد عبر مكالمة صوتية أو واتساب.',
  },
];
