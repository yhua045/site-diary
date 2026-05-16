import type { AttachmentDto } from '../../api/types'

interface InlineImagesProps {
  images: AttachmentDto[]
  onImageClick?: (id: number) => void
}

const MAX_INLINE = 3

export function InlineImages({ images, onImageClick }: InlineImagesProps) {
  if (images.length === 0) return null

  const shown = images.slice(0, MAX_INLINE)
  const overflow = images.length - MAX_INLINE
  const extra = images[MAX_INLINE] // 4th image for the overlay, if any

  return (
    <div className="mt-3 grid grid-cols-3 gap-2">
      {shown.map((img) => (
        <button
          key={img.id}
          type="button"
          onClick={() => onImageClick?.(img.id)}
          className="rounded overflow-hidden focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <img
            src={img.fileUrl}
            alt={img.fileName}
            className="w-full max-h-48 object-cover"
            loading="lazy"
          />
        </button>
      ))}
      {overflow > 0 && extra && (
        <button
          type="button"
          onClick={() => onImageClick?.(extra.id)}
          className="relative rounded overflow-hidden focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <img
            src={extra.fileUrl}
            alt={extra.fileName}
            className="w-full max-h-48 object-cover opacity-40"
            loading="lazy"
          />
          <span className="absolute inset-0 flex items-center justify-center text-xl font-bold text-white drop-shadow">
            +{overflow}
          </span>
        </button>
      )}
    </div>
  )
}
