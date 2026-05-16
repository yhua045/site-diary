import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DiaryCreateForm } from '../../components/diary/DiaryCreateForm'
import type { DiaryTemplate } from '../../api/types'

const mockTemplate: DiaryTemplate = {
  id: 1,
  name: 'Standard',
  sections: [
    {
      id: 'sec1',
      label: 'General',
      fields: [
        { id: 'weather', label: 'Weather', type: 'text', required: true, placeholder: 'e.g. Sunny' },
        { id: 'workers', label: 'Workers on site', type: 'number', required: false },
        { id: 'notes', label: 'Notes', type: 'text', required: false },
      ],
    },
  ],
}

describe('DiaryCreateForm', () => {
  const onSubmit = vi.fn()
  const onClose = vi.fn()

  beforeEach(() => {
    onSubmit.mockReset()
    onClose.mockReset()
  })

  it('renders form title', () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={false} />)
    expect(screen.getByText(/New Diary Entry/i)).toBeInTheDocument()
  })

  it('renders all template fields', () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={false} />)
    expect(screen.getByLabelText(/Weather/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Workers on site/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Notes/i)).toBeInTheDocument()
  })

  it('shows validation error when required field is empty on submit', async () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={false} />)
    const saveBtn = screen.getByRole('button', { name: /save entry/i })
    await userEvent.click(saveBtn)
    await waitFor(() => {
      expect(screen.getByText(/weather is required/i)).toBeInTheDocument()
    })
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('calls onSubmit with payload when all required fields filled', async () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={false} />)
    const weatherInput = screen.getByLabelText(/Weather/i)
    await userEvent.type(weatherInput, 'Sunny, 22°C')

    const saveBtn = screen.getByRole('button', { name: /save entry/i })
    await userEvent.click(saveBtn)

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledOnce()
    })
    const [[callArgs]] = onSubmit.mock.calls as [[{ payload: Record<string, unknown>; date: string }]]
    expect(callArgs.payload['weather']).toBe('Sunny, 22°C')
    expect(callArgs.date).toBeTruthy()
  })

  it('calls onClose when Cancel button clicked', async () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={false} />)
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }))
    expect(onClose).toHaveBeenCalledOnce()
  })

  it('disables Save button while submitting', () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={true} />)
    expect(screen.getByRole('button', { name: /save entry/i })).toBeDisabled()
  })

  it('renders a date field defaulting to today', () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={false} />)
    const dateInput = screen.getByLabelText(/date/i)
    expect(dateInput).toBeInTheDocument()
    // Should have a value (today)
    expect((dateInput as HTMLInputElement).value).toBeTruthy()
  })

  it('renders file upload area', () => {
    render(<DiaryCreateForm template={mockTemplate} onSubmit={onSubmit} onClose={onClose} isSubmitting={false} />)
    expect(screen.getByText(/add photo|attach|upload/i)).toBeInTheDocument()
  })
})
