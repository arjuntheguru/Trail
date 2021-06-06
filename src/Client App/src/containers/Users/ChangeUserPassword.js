import React from 'react';
import { Modal } from 'react-bootstrap';
import ChangePasswordForm from '../Account/ChangePasswordForm';

const ChangeUserPassword = ({ show, onClose, data }) => {
    
    return (
        <Modal show={show} onHide={onClose} centered size="lg" className="bg-transparent">
            <Modal.Body className="bgGradient">{<ChangePasswordForm data={data} onClose={onClose}/>}</Modal.Body>
        </Modal>
    )

}

export default ChangeUserPassword;